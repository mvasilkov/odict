﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using DawgSharp;
using Zalizniak;

namespace odict.ru
{
    /// <summary>
    /// Summary description for Api
    /// </summary>
    public class Api : IHttpHandler
    {
        [DataContract]
        protected class LineForms
        {
            [DataMember]
            public string Line;
            [DataMember]
            public string[] Forms;
        }

        System.Web.HttpContext Context;

        public Api()
        {
            Context = System.Web.HttpContext.Current;
        }
        
        protected void GetForms(string lemma, string rule)
        {            
            int StressPos = lemma.Trim().IndexOf('*');

            string GramInfo = rule.Substring(0, rule.IndexOf("="));

            string Line = lemma.Replace("*", String.Empty).ToLowerInvariant() + " " + 
                (StressPos == -1 ? "?" : StressPos.ToString()) + " " + GramInfo.Substring(GramInfo.IndexOf(' '));

            WriteJSONToResponse(new LineForms()
                {
                    Line = Line,
                    Forms = FormGenerator.GetAccentedForms(Line, delegate { })
                        .Select(Func => HttpUtility.HtmlEncode(Func.AccentedForm)).ToArray()
                });
        }

        protected void GetRules(string prefixText)
        {
            string PrefixText = prefixText.Replace("*", String.Empty);

            Dawg<string> Dawg = Dawg<string>.Load(new MemoryStream(Resources.zalizniak), 
                Func => 
                { 
                    string s = Func.ReadString(); 
                    return s == String.Empty ? null : s; 
                });

            int PrefixLen = Dawg.GetLongestCommonPrefixLength(PrefixText.Reverse());

            WriteJSONToResponse(Dawg.MatchPrefix(PrefixText.Reverse().Take(PrefixLen))
                .GroupBy(kvp => kvp.Value, kvp => kvp)
                .SelectMany(g => g.Take(1))
                .Select(kvp => kvp.Value + "=" + new string(kvp.Key.Reverse().ToArray()))
                .Take(10)
                .ToArray());
        }

        protected void WriteJSONToResponse<T>(T obj)
        {
            Context.Response.ContentType = "application/json";

            MemoryStream RespData = new MemoryStream();

            DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(T));
            Serializer.WriteObject(RespData, obj);

            using (StreamReader Reader = new StreamReader(RespData))
            {
                RespData.Seek(0, SeekOrigin.Begin);
                Context.Response.Write(Reader.ReadToEnd());
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            string Action = context.Request.Params["action"];

            if (!String.IsNullOrEmpty(Action))
            {
                switch (Action.ToLower())
                {
                    case "getrules":
                        string PrefixText = context.Request.Params["prefixtext"];
                        if (PrefixText != null)
                        {
                            GetRules(PrefixText);
                            return;
                        }
                        break;
                    case "getforms":
                        string Lemma = context.Request.Params["lemma"];
                        string Rule = context.Request.Params["rule"];
                        if (!String.IsNullOrEmpty(Lemma) && !String.IsNullOrEmpty(Rule))
                        {
                            GetForms(Lemma, Rule);
                            return;
                        }
                        break;
                    default:
                        break;
                }
            }

            context.Response.Write("Invalid parameters");
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}