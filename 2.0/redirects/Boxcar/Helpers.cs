using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Xml;

namespace RedirectHelpers
{
    public static class Misc
    {
        public static XmlNode createXMLElement(XmlDocument source, string name, string innerText)
        {
            XmlNode aNode = source.CreateElement(name);
            aNode.InnerText = innerText;
            return aNode;
        }

        public static Tuple<string, Dictionary<string, string>> getQueryParameters(string httpQuery)
        {
            if (httpQuery.Contains('?'))
            {
                string[] tmp = httpQuery.Split('?');
                // any parameters?
                if (!string.IsNullOrEmpty(tmp[1]))
                {
                    // yes
                    Dictionary<string, string> parameters = new Dictionary<string, string>();
                    string[] args = tmp[1].Split('&');
                    foreach (string arg in args)
                    {
                        if (arg.Contains('='))
                        {
                            string[] tmp2 = arg.Split('=');
                            parameters.Add(tmp2[0], tmp2[1]);
                        }
                        else
                        {
                            parameters.Add(arg, "");
                        }
                    }
                    return new Tuple<string, Dictionary<string, string>>(tmp[0], parameters);
                }
                else
                {
                    // no parameters
                    return new Tuple<string, Dictionary<string, string>>(tmp[0], new Dictionary<string, string>());
                }
            }
            else
            {
                // just return what was passed with an empty dictionary
                return new Tuple<string, Dictionary<string, string>>(httpQuery, new Dictionary<string, string>());
            }
        }

        public static int versionMajor(Assembly assembly)
        {
            //Assembly.GetExecutingAssembly().Location
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileMajorPart;
        }

        public static int versionMinor(Assembly assembly)
        {
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileMinorPart;
        }

        public static string userAgentString(Assembly assembly, string userAgent)
        {
            return userAgent + "/" + versionMajor(assembly) + "/" + versionMinor(assembly);
        }

        public static string safeGetInnerText(XmlNode node, string alt = "")
        {
            if (node == null)
            {
                return alt;
            }
            else
            {
                return node.InnerText;
            }
        }

        public static void safeSetInnerText(XmlNode node, string nodeName, string innerText)
        {
            if (node != null)
            {
                XmlNode childNode = node[nodeName];
                if (childNode == null)
                {
                    childNode = node.OwnerDocument.CreateElement(nodeName);
                    node.AppendChild(childNode);
                }
                childNode.InnerText = innerText;
            }
        }

        public static string safeGetValue(Dictionary<string, string> contents, string key, string alt = "")
        {
            if (contents == null)
            {
                return alt;
            }
            else if (contents.ContainsKey(key))
            {
                return contents[key];
            }
            else
            {
                return alt;
            }
        }

        public static string Pathify(string path)
        {
            if (!path.EndsWith("\\"))
                return path += "\\";

            else
                return path;

        }

        public static string translateTemplate(string templateString, string appName)
        {
            string aStr = templateString;
            aStr = aStr.Replace("{app}", appName);
            aStr = aStr.Replace("{computer}", Environment.MachineName);
            aStr = aStr.Replace("{computer-lower}", Environment.MachineName.ToLower());
            aStr = aStr.Replace("{computer-upper}", Environment.MachineName.ToUpper());
            return aStr;
        }

    }

}
