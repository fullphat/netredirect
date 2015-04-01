using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Net;
using System.Web;
using System.Xml;
using netredirect;
using libSnarlStyles;
using RedirectHelpers;

namespace prowl4
{
    class prowl4 : Redirect  
    {
        private const string LIB_DATE = "1-Apr-2015";
        RedirectInfo myInfo = new RedirectInfo();
        XmlDocument configFile = new XmlDocument();
        string configPath = "";

        // == internal funcs ==

        private void _buildSchemeList()
        {
            myInfo.schemes = new Dictionary<string, string>();
            XmlNode schemes = _getSchemesNode();
            foreach (XmlNode node in schemes)
            {
                myInfo.schemes.Add(node["guid"].InnerText, node["name"].InnerText);
            }
        }

        private XmlNode _getSchemesNode()
        {
            XmlNode root = configFile.SelectSingleNode("config");
            if (root != null)
            {
                return root.SelectSingleNode("schemes");
            }
            else
            {
                return null;
            }
        }

        private XmlNode _findNodeWithGuid(string guid)
        {
            foreach (XmlNode node in _getSchemesNode())
            {
                if (node["guid"].InnerText == guid)
                {
                    return node;
                }
            }
            return null;
        }


        // == Overrides ==

        
        public override RedirectInstance CreateInstance()
        {
            return new ProwlInstance(configFile);
        }

        public override WebAdminReply DoWebAdmin(WebAdminRequest request)
        {
            if (request.Command == "")
            {
                // font page
                StringBuilder schemes = new StringBuilder();
                XmlNode schemesNode = _getSchemesNode();
                foreach (XmlNode node in schemesNode)
                {
                    schemes.Append("<tr>");
                    schemes.Append("<td nowrap><a href='!link!/show?scheme=" + node["guid"].InnerText + "'>" + node["name"].InnerText + "</a></td>");
                    schemes.Append("<td nowrap>" + Misc.safeGetInnerText(node["source"]) + "</td>");
                    if (schemesNode.ChildNodes.Count > 1)
                    {
                        schemes.Append("<td nowrap>[ <a href='!link!/delete?scheme=" + node["guid"].InnerText + "'>Delete</a> ]</td>");
                    }
                    else
                    {
                        schemes.Append("<td></td>");
                    }
                    schemes.Append("</tr>");
                }

                StringBuilder reply = new StringBuilder();
                reply.Append("<h3>Prowl Settings</h3>");
                reply.Append("<p>Sends notifications to iOS devices using Prowl (http://www.prowlapp.com).</p>");
                reply.Append("<table width='90%'><tr><th>Name</th><th>Source Template</th><th>Options</th></tr>");
                reply.Append(schemes.ToString());
                reply.Append("<tr><td colspan='3'><a href='!link!/add'><b>Add new scheme...</b></a></td></tr>");
                reply.Append("</table>");
                return new WebAdminReply((int)HttpStatusCode.OK, reply.ToString());
            }
            else
            {
                // get the query content
                StringBuilder reply = null;
                switch (request.Command)
                {
                    case "add":
                        // no args
                        XmlNode newNode = configFile.CreateElement("scheme");
                        newNode.AppendChild(Misc.createXMLElement(configFile, "guid", Guid.NewGuid().ToString()));
                        newNode.AppendChild(Misc.createXMLElement(configFile, "name", "New scheme"));
                        newNode.AppendChild(Misc.createXMLElement(configFile, "api_key", "{enter your api key here}"));
                        newNode.AppendChild(Misc.createXMLElement(configFile, "source", "{app} on {computer}"));

                        XmlNode schemesNode = _getSchemesNode();
                        schemesNode.AppendChild(newNode);
                        configFile.Save(this.configPath);
                        // re-create myInfo->Schemes content
                        _buildSchemeList();
                        // return <httpStatus, reply HTML, redirect URL>
                        return new WebAdminReply((int)HttpStatusCode.Created, "<h3>Adding new scheme...</h3>", "!link!/show?scheme=" + newNode["guid"].InnerText);

                    case "show":
                        //show?scheme=guid
                        if (request.Parameters.ContainsKey("scheme"))
                        {
                            XmlNode theNode = _findNodeWithGuid(request.Parameters["scheme"]);

                            reply = new StringBuilder();
                            reply.Append("<h3>Edit Scheme</h3><form action='!link!/update" + "'>");
                            reply.Append("<table width='60%'>");
                            reply.Append("<input type='hidden' name='guid' value='" + Misc.safeGetInnerText(theNode["guid"]) + "'>");
                            reply.Append("<tr><td><b>Scheme name:</b></td><td><input type='text' size='60' name='name' value='" + Misc.safeGetInnerText(theNode["name"]) + "'></td></tr>");
                            reply.Append("<tr><td><b>Access token:</b></td><td><input type='text' size='60' name='apikey' value='" + Misc.safeGetInnerText(theNode["api_key"]) + "'></td></tr>");
                            reply.Append("<tr><td><b>Source name:</b></td><td><input type='text' size='60' name='source' value='" + Misc.safeGetInnerText(theNode["source"], "{app} on {computer}") + "'></td></tr>");
                            //reply.Append("");

                            //"<tr><td><b>Application Name:</b></td><td><input type='text' size='60' name='apptext' value='" & ps.GetValueWithDefault("apptext", "") & "'><p class='small'>Prowl supports an entry which indicates the sending application.  You can use the special values %APP% and %COMPUTER% to indicate the sending application and the name of this computer respectively.</p></td></tr>" & _
                            //uAddBool("showpriorityonly", "Only process high priority notifications?", ps) & _
                            //uAddBool("replacecrlf", "Remove line feeds?", ps) & _
                            //uAddBool("redactifsensitive", "Redact content if marked sensitive?", ps) & _

                            reply.Append("<tr><td colspan='2'><input type='submit' value='Submit'></td></tr>");
                            reply.Append("</table></form>");

                            return new WebAdminReply((int)HttpStatusCode.OK, reply.ToString());
                        }
                        else
                        {
                            // not found
                            return new WebAdminReply((int)HttpStatusCode.NotFound, "Scheme not found.");
                        }

                    case "update":
                        // update?guid=guid&args...
                        XmlNode nodeUpdate = _findNodeWithGuid(request.Parameters["guid"]);
                        if (nodeUpdate != null)
                        {
                            Misc.safeSetInnerText(nodeUpdate, "name", HttpUtility.UrlDecode(request.Parameters["name"]));
                            Misc.safeSetInnerText(nodeUpdate, "api_key", HttpUtility.UrlDecode(request.Parameters["apikey"]));
                            Misc.safeSetInnerText(nodeUpdate, "source", HttpUtility.UrlDecode(request.Parameters["source"]));
                            configFile.Save(configPath);
                            _buildSchemeList();
                            return new WebAdminReply((int)HttpStatusCode.ResetContent, "<h3>Changes saved</h3>", "!link!");
                        }
                        else
                        {
                            // not found
                            return new WebAdminReply((int)HttpStatusCode.NotFound, "Scheme not found.");
                        }

                    case "delete":
                        // delete?scheme=guid
                        if (_getSchemesNode().ChildNodes.Count < 2)
                        {
                            // must have at lease one scheme
                            return new WebAdminReply((int)HttpStatusCode.MethodNotAllowed, "Must have at least one scheme.");
                        }
                        else
                        {
                            XmlNode nodeToDelete = _findNodeWithGuid(request.Parameters["scheme"]);
                            if (nodeToDelete == null)
                            {
                                // not found
                                return new WebAdminReply((int)HttpStatusCode.NotFound, "Scheme not found.");
                            }
                            else
                            {
                                // delete, rebuild scheme list and return "changed"
                                nodeToDelete.ParentNode.RemoveChild(nodeToDelete);
                                configFile.Save(configPath);
                                _buildSchemeList();
                                return new WebAdminReply((int)HttpStatusCode.ResetContent, "<h3>Scheme deleted</h3>", "!link!");
                            }
                        }

                    default:
                        return new WebAdminReply((int)HttpStatusCode.NotFound, "Unknown commnand.");
                }
            }
        }

        public override RedirectInfo GetInfo()
        {
            return myInfo;
        }

        public override bool Load()
        {
            // do we have a config?  if so, load it up, otherwise create one
            this.configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), this.BaseName + ".conf");
            try
            {
                configFile.Load(this.configPath);
            }
            catch (FileNotFoundException)
            {
                XmlTextWriter w = new XmlTextWriter(configPath, Encoding.UTF8);
                w.WriteStartDocument();
                w.WriteStartElement("config");
                w.WriteStartElement("schemes");

                // create single (empty) scheme
                w.WriteStartElement("scheme");
                w.WriteElementString("guid", Guid.NewGuid().ToString());
                w.WriteElementString("name", "Default");
                w.WriteElementString("api_key", "");
                w.WriteEndElement();

                w.WriteEndElement();                // close schemes
                w.WriteEndElement();                // close config
                w.WriteEndDocument();
                w.Close();

                configFile.Load(configPath);

            }

            // build scheme list
            _buildSchemeList();

            // build rest of info
            myInfo.date = LIB_DATE;
            myInfo.description = "Sends notifications to iOS devices using Prowl (http://www.prowlapp.com)";
            myInfo.isWebRedirect = true;
            myInfo.name = "Prowl.net";
            myInfo.supportsWebAdmin = true;
            myInfo.vendorCopyright = "Copyright © 2015 full phat products";
            myInfo.vendorEmail = "myInfo@fullphat.net";
            myInfo.versionMajor = Misc.versionMajor(Assembly.GetExecutingAssembly());
            myInfo.versionMinor = Misc.versionMinor(Assembly.GetExecutingAssembly());
            myInfo.vendorURL = "http://getsnarl.info";

            return true;
        }
    }


    class ProwlInstance : RedirectInstance
    {
        private const string API_BASE = "https://api.prowlapp.com/publicapi/";
        private const string METHOD_ADD = "add";
        private XmlDocument _config;

        public ProwlInstance(XmlDocument configFile)
        {
            _config = configFile;
        }

        public override void SendContent(string appName, string schemeName, Dictionary<string, string> notificationContent, string notificationIcon, int notificationPriority)
        {
            XmlNode root = _config.SelectSingleNode("config");
            root = root.SelectSingleNode("schemes");
            XmlNode scheme = null;

            foreach (XmlNode node in root)
            {
                if (node["guid"].InnerText == schemeName)
                {
                    scheme = node;
                    break;
                }
            }

            StringBuilder body = new StringBuilder("apikey=" + scheme["api_key"].InnerText);
            body.Append("&application=" + HttpUtility.UrlEncode(Misc.translateTemplate(Misc.safeGetInnerText(scheme["source"]), appName)));
            body.Append("&description=" + HttpUtility.UrlEncode(Misc.safeGetValue(notificationContent, "title").Replace("\r", "")));
            body.Append("&event=" + HttpUtility.UrlEncode(Misc.safeGetValue(notificationContent, "text").Replace("\r", "")));
            body.Append("&priority=" + notificationPriority.ToString());

            if (notificationContent.ContainsKey("callback"))
            {
                body.Append("&url=" + HttpUtility.UrlEncode(notificationContent["callback"]));
            }

            //body.AppendFormat("apikey={0}&application={1}&description={2}&event={3}&priority={4}",
            //                  HttpUtility.UrlEncode(API_KEY_TEMP),
            //                  HttpUtility.UrlEncode(appName),
            //                  HttpUtility.UrlEncode(Helpers.SafeGetValue(notificationContent, "title").Replace("\r", "")),
            //                  HttpUtility.UrlEncode(Helpers.SafeGetValue(notificationContent, "text").Replace("\r", "")),
            //                  notificationPriority.ToString());

            byte[] content = Encoding.UTF8.GetBytes(body.ToString());

            HttpWebRequest addRequest = HttpWebRequest.Create(API_BASE + METHOD_ADD) as HttpWebRequest;
            addRequest.Method = "POST";
            addRequest.UserAgent = Misc.userAgentString(Assembly.GetExecutingAssembly(), "SnarlProwlRedirect");
            addRequest.Headers.Add("X-Clacks-Overhead: GNU Terry Pratchett");
            addRequest.ContentType = "application/x-www-form-urlencoded";
            addRequest.ContentLength = content.Length;

            Stream dataStream = addRequest.GetRequestStream();
            dataStream.Write(content, 0, content.Length);
            dataStream.Close();

            WebResponse postResponse = default(WebResponse);

            try
            {
                postResponse = addRequest.GetResponse();
            }
            finally
            {
                if (postResponse != null)
                    postResponse.Close();
            }
        }
    }
}
