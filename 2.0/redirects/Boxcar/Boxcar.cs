using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Net;
using System.Web;
using System.IO;
using System.Xml;
using System.Configuration;
using System.Collections;
using RedirectHelpers;
using netredirect;

namespace boxcar
{
    public class boxcar : Redirect 
    {
        private const string LIB_DATE = "29-Mar-2015";
        RedirectInfo myInfo = new RedirectInfo();
        XmlDocument configFile = new XmlDocument();
        string configPath = "";

        // == internal functions ==

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

        // == overrides ==

        public override RedirectInstance CreateInstance()
        {
            return new BoxcarInstance(configFile);
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
                reply.Append("<h3>Boxcar Settings</h3>");
                reply.Append("<p>Sends notifications to your <a href='https://boxcar.io'>Boxcar</a> inbox.</p>");
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
                            return new WebAdminReply((int)HttpStatusCode.MethodNotAllowed, "Must have at least one scheme.", "");
                        }
                        else
                        {
                            XmlNode nodeToDelete = _findNodeWithGuid(request.Parameters["scheme"]);
                            if (nodeToDelete == null)
                            {
                                // not found
                                return new WebAdminReply((int)HttpStatusCode.NotFound, "Scheme not found.", "");
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
                        reply = new StringBuilder();
                        reply.Append("command=" + request.Command + "<br>");
                        foreach (KeyValuePair<string, string> kvp in request.Parameters)
                        {
                            reply.Append("param:" + kvp.Key + " == " + kvp.Value + "<br>"); 
                        }
                        return new WebAdminReply((int)HttpStatusCode.NotFound, reply.ToString());
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

            //this.configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            //                               "full phat\\snarl\\redirects.net", "boxcar", "boxcar.conf");

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
            myInfo.description = "Sends notifications to your Boxcar inbox";
            myInfo.isConfigurable = false;              // enable this if you just want to display a UI
            myInfo.isWebRedirect = true;
            myInfo.name = "Boxcar";
            myInfo.supportsWebAdmin = true;             // enable this if you want web admin functionality
            myInfo.vendorCopyright = "Copyright © 2015 full phat products";
            myInfo.vendorEmail = "info@fullphat.net";
            myInfo.vendorURL = "http://www.fullphat.net";
            myInfo.versionMajor = Misc.versionMajor(Assembly.GetExecutingAssembly());
            myInfo.versionMinor = Misc.versionMinor(Assembly.GetExecutingAssembly());

            return true;
        }

        public override void Unload()
        {
            base.Unload();
        }
    }


    class BoxcarInstance : RedirectInstance
    {
        private const string API_BASE = "https://new.boxcar.io/api/notifications";
        private XmlDocument _config;

        public BoxcarInstance(XmlDocument configFile)
        {
            _config = configFile;
        }

        public override void SendContent(string appName, string schemeName, Dictionary<string, string> notificationContent, string notificationIcon, int notificationPriority)
        {
            //user_credentials: This is where you pass your access token. Your access token can be found in Boxcar global setting pane. It is a string composed of letters and numbers. Do not confuse it with your Boxcar email address.
            //notification[title]: This parameter will contain the content of the alert and the title of the notification in Boxcar. Max size is 255 chars.
            //notification[long_message]: This is where you place the content of the notification itself. It can be either text or HTML. Max size is 4kb.
            //notification[source_name] (optional): This is a short source name to show in inbox. Default is "Custom notification".
            //notification[open_url] (optional): If defined, Boxcar will redirect you to this url when you open the notification from the Notification Center. It can be a http link like http://maps.google.com/maps?q=cupertino or an inapp link like twitter:///user?screen_name=vutheara﻿﻿
            //notification[icon_url] (optional): This is where you define the icon you want to be displayed within the application inbox﻿.

            //notification[sound] (optional):This is were you define the sound you want to play on your device. As a default, the general sound is used, if you omit this parameter. General sound typically default to silent, but if you changed it, you can force the notification to be silent with the "no-sound" sound name.

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

            StringBuilder body = new StringBuilder("user_credentials=" + scheme["api_key"].InnerText);
            body.Append("&notification[title]=" + HttpUtility.UrlEncode(Misc.safeGetValue(notificationContent, "title")));
            body.Append("&notification[long_message]=" + HttpUtility.UrlEncode(Misc.safeGetValue(notificationContent, "text")));
            body.Append("&notification[source_name]=" + HttpUtility.UrlEncode(Misc.translateTemplate(Misc.safeGetInnerText(scheme["source"]), appName)));

            if (notificationContent.ContainsKey("callback"))
            {
                body.Append("&notification[open_url]=" + HttpUtility.UrlEncode(notificationContent["callback"]));
            }

            if (notificationIcon.StartsWith("http://") || notificationIcon.StartsWith("https://"))
            {
                body.Append("&notification[icon_url]=" + HttpUtility.UrlEncode(notificationIcon));
            }

            byte[] content = Encoding.UTF8.GetBytes(body.ToString());

            HttpWebRequest addRequest = HttpWebRequest.Create(API_BASE) as HttpWebRequest;
            addRequest.Method = "POST";
            addRequest.UserAgent = Misc.userAgentString(Assembly.GetExecutingAssembly(), "SnarlBoxcarRedirect" );
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

//string sourceTemplate = Misc.safeGetInnerText(scheme["source"]);
//sourceTemplate = sourceTemplate.Replace("{app}", appName);
//sourceTemplate = sourceTemplate.Replace("{computer}", Environment.MachineName);
//sourceTemplate = sourceTemplate.Replace("{computer-lower}", Environment.MachineName.ToLower());
//sourceTemplate = sourceTemplate.Replace("{computer-upper}", Environment.MachineName.ToUpper());
//body.Append("&notification[source_name]=" + HttpUtility.UrlEncode(sourceTemplate));
