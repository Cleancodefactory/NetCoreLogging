using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ccf.Ck.Libs.Logging
{
    public class LoggerViewPreprocessor
    {
        private static readonly Regex _HtmlTemplateReplace =
            new Regex(
                @"\{\{\s*([a-zA-Z-._]+)\s*\:\s*\{(.*)\}\s*\}\}",
                RegexOptions.Compiled);

        private static readonly Regex _HtmlValueReplace =
            new Regex(
                @"\{(value)\}",
                RegexOptions.Compiled);

        private static readonly Regex _RowCount =
            new Regex(
                @"(\s)(\<input type='hidden'(.*)\>)",
                RegexOptions.Compiled);

        public string View { get; set; }
        object Data { get; set; }

        public LoggerViewPreprocessor(string rawView, object rawData)
        {
            this.Data = rawData;
            this.View = rawView;
        }

        public void Pages()
        {
            this.View = _RowCount.Replace(this.View, m =>
            {
                if (!m.Success)
                {
                    return m.Value;
                }

                //string template = m.Groups[2].Value;
                StringBuilder builder = new StringBuilder();
                if (this.Data != null)
                {
                    if (int.TryParse(this.Data.ToString(), out int pageCount))
                    {
                        builder.AppendFormat(@" <input type='hidden' id='allPages' value='{0}'>", pageCount);
                    }
                }
                return builder.ToString();
            });
        }

        public void GenerateTable()
        {
            this.View = _HtmlTemplateReplace.Replace(this.View, m =>
            {
                if (!m.Success)
                {
                    return m.Value;
                }

                string resourceKey = m.Groups[1].Value;
                string template = m.Groups[2].Value;

                StringBuilder builder = new StringBuilder();
                List<Dictionary<string, object>> temp = this.Data as List<Dictionary<string, object>>;
                if (temp != null && temp.Count > 0)
                {
                    switch (resourceKey)
                    {
                        case "header-expr":
                            foreach (string s in temp[0].Keys.ToList())
                            {
                                builder.AppendLine(_HtmlValueReplace.Replace(template, n =>
                                {
                                    if (!n.Success)
                                    {
                                        return n.Value;
                                    }
                                    return s;
                                }));
                            }
                            break;
                        case "row-expr":
                            foreach (Dictionary<string, object> t in temp)
                            {
                                builder.AppendLine("<tr>");
                                foreach (KeyValuePair<string, object> kvp in t)
                                {
                                    builder.Append(_HtmlValueReplace.Replace(template, n =>
                                    {
                                        if (!n.Success)
                                        {
                                            return n.Value;
                                        }
                                        return kvp.Value.ToString();
                                    }));
                                }
                                builder.AppendLine("</tr>");
                            }
                            break;
                    }
                }
                return builder.ToString();
            });
        }
    }

}
