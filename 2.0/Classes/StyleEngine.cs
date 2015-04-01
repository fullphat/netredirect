using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using libSnarlStyles;
using melon;

namespace netredirect
{
    
    public class RedirectInfo
    {
        public string name { get; set; }
        public string description { get; set; }
        public string date { get; set; }
        public string vendorCopyright { get; set; }
        public string vendorURL { get; set; }
        public string vendorEmail { get; set; }
        public int versionMajor { get; set; }
        public int versionMinor { get; set; }
        public bool isConfigurable { get; set; }
        public bool isWebRedirect { get; set; }
        public bool supportsWebAdmin { get; set; }

        public Dictionary<string,string> schemes { get; set; }

        public RedirectInfo()
        {
            name = "";
            description = "";
            date = "";
            vendorCopyright = "";
            vendorURL = "";
            vendorEmail = "";
            versionMajor = 0;
            versionMinor = 0;
            isConfigurable = false;
            isWebRedirect = false;
            supportsWebAdmin = false;
            schemes = new Dictionary<string, string>();
        }
    }


    public class WebAdminRequest
    {
        public string Command { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }

        public WebAdminRequest(string command)
        {
            this.Command = command;
            this.Parameters = new Dictionary<string, string>();
        }
    }


    public class WebAdminReply
    {
        public int StatusCode { get; private set; }
        public string ReplyHTML { get; private set; }
        public string RedirectURL { get; private set; }

        public WebAdminReply()
        {
            StatusCode = 400;
            ReplyHTML = "";
            RedirectURL = "";
        }

        public WebAdminReply(int statusCode, string replyHtml, string redirectUrl = "")
        {
            this.StatusCode = statusCode;
            this.ReplyHTML = replyHtml;
            this.RedirectURL = redirectUrl;
        }

    }

    // represents a loaded .net redirect object

    public class Redirect 
    {
        public string BaseName { get; set; }

        public string StyleName
        {
            get
            {
                return GetInfo().name;
            }
        }

        public Redirect()
        {
            BaseName = "";
        }

        public virtual bool Load()
        {
            return false;       
        }

        public virtual void Unload() { }

        public virtual RedirectInfo GetInfo()
        {
            return new RedirectInfo();
        }

        public virtual WebAdminReply DoWebAdmin(WebAdminRequest request)
        {
            return new WebAdminReply();
        }

        public virtual RedirectInstance CreateInstance()
        {
            return new RedirectInstance();
        }

    }


    // RedirectInstance -- Wraps IStyleInstance

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class RedirectInstance : IStyleInstance
    {
        // Class methods

        public RedirectInstance()
        {
        }

        private string _SafeGetValue(Dictionary<string, string> contents, string key, string alt = "")
        {
            if (contents.ContainsKey(key))
            {
                return contents[key];
            }
            else
            {
                return alt;
            }
        }

        public virtual void SendContent(string appName, string schemeName, Dictionary<string,string> notificationContent, string notificationIcon, int notificationPriority) { }

        // helpers

        private Dictionary<string, string> _packedStrToDictionary(string packedString)
        {
            Dictionary<string, string> contents = new Dictionary<string, string>();
            string[] itemSplit = new string[] { "#?" };
            string[] argSplit = new string[] { "::" };

            if (!string.IsNullOrEmpty(packedString))
            {
                string[] items = packedString.Split(itemSplit, StringSplitOptions.None);
                foreach (string entry in items)
                {
                    if (entry.Contains("::"))
                    {
                        string[] item = entry.Split(argSplit, StringSplitOptions.None);
                        contents.Add(item[0], item[1]);
                    }
                }
            }
            return contents;
        }


        // IStyleInstance

        [ComVisible(true)]
        void IStyleInstance.AdjustPosition(ref int x, ref int y, ref short Alpha, ref bool Done)
        {
            // not used
        }

        [ComVisible(true)]
        MImage IStyleInstance.GetContent()
        {
            // not used
            return null;
        }

        [ComVisible(true)]
        bool IStyleInstance.Pulse()
        {
            // not used
            return false;
        }

        [ComVisible(true)]
        void IStyleInstance.Show(bool Visible)
        {
            // not used
        }

        [ComVisible(true)]
        void IStyleInstance.UpdateContent(ref notification_info NotificationInfo)
        {
            // translate the incoming notification_info into something more useful, then call 
            // sendContent() which can be overridden by implementing classes...
            Dictionary<string, string> content = _packedStrToDictionary(NotificationInfo.Text);
            string strPriority = _SafeGetValue(content, "priority", "0");
            int intPriority = 0;

            try
            {
                intPriority = int.Parse(strPriority);
            }
            catch
            {
            }

            this.SendContent(NotificationInfo.Title, NotificationInfo.Scheme, content, NotificationInfo.Icon, intPriority);
        }

    }

    // Style engine -- loads up .net redirects and presents them back out as IStyleInstances

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class StyleEngine : IStyleEngine 
    {
        private const string LIB_DATE = "30-Mar-2015";
        private const int LIB_VERSION = 46;
        private const int LIB_REVISION = 247;

        private List<Redirect> loadedStyles = new List<Redirect>();
        private string lastResponse = "";
        private string lastRedirect = "";
        private string bootPath = "";
        private string redirectsPath = "";

        public StyleEngine()
        {
            //Stream myFile = File.Create(Path.Combine(Path.GetTempPath(), "netredirect.debug"));
            //TextWriterTraceListener myTextListener = new TextWriterTraceListener(myFile);
            //Trace.Listeners.Add(myTextListener);

            this.bootPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            this.redirectsPath = Path.Combine(this.bootPath, "redirects.net");
            Trace.WriteLine("running from {" + this.bootPath + "}");
            Trace.WriteLine("search folder {" + this.redirectsPath + "}");

        }


        private WebAdminRequest _GetQueryParameters(string httpQuery)
        {
            if (httpQuery.Contains('?'))
            {
                string[] tmp = httpQuery.Split('?');
                // any parameters?
                if (!string.IsNullOrEmpty(tmp[1]))
                {
                    // yes
                    WebAdminRequest request = new WebAdminRequest(tmp[0]);
                    string[] args = tmp[1].Split('&');
                    foreach (string arg in args)
                    {
                        if (arg.Contains('='))
                        {
                            string[] tmp2 = arg.Split('=');
                            request.Parameters.Add(tmp2[0], tmp2[1]);
                        }
                        else
                        {
                            request.Parameters.Add(arg, "");
                        }
                    }
                    return request;
                }
                else
                {
                    // no parameters
                    return new WebAdminRequest(tmp[0]);
                }
            }
            else
            {
                // just return what was passed with an empty dictionary
                return new WebAdminRequest(httpQuery);
            }
        }




        private bool _loadRedirect(string sourcePath)
        {
            bool result = false;
            //MessageBox.Show(">>" + sourcePath);
            // enumerate the passed folder grabbing just the filename in lowercase
            List<string> newContents = new List<string>();
            foreach (string str in Directory.EnumerateFiles(sourcePath, "*.*"))
            {
                //MessageBox.Show(str);
                newContents.Add(Path.GetFileName(str).ToLower());
            }

            string baseName = Path.GetFileName(sourcePath);
            string dllName = baseName + ".dll";
            //MessageBox.Show("dllname>" + dllName);
            if (newContents.Contains("icon.png") && newContents.Contains(dllName))
            {
                // has dll and icon - that'll do for now
                try
                {
                    Assembly assembly = Assembly.LoadFrom(Path.Combine(sourcePath, dllName));
                    try
                    {
                        Redirect redirect = (Redirect)assembly.CreateInstance(baseName + "." + baseName, true);
                        redirect.BaseName = baseName;
                        if (redirect.Load())
                        {
                            loadedStyles.Add(redirect);
                            result = true;
                        }
                        else
                        {
                            //MessageBox.Show("redirect.Load()");
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("create instance " + baseName + "." + baseName + ">" + e.Message);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("loadfrom>" + e.Message);
                }
            }
            return result;
        }

        [ComVisible(true)]
        public void ToTheBridgeToTheBridgeToTheBridgeNow()
        {
        }

        // IStyleEngine

        [ComVisible(true)]
        int IStyleEngine.CountStyles()
        {
            return this.loadedStyles.Count;
        }

        [ComVisible(true)]
        IStyleInstance IStyleEngine.CreateInstance(string StyleName)
        {
            // this would look up StyleName in the loadedStyles list
            // if found, it will create a new instance of the Redirect
            Redirect item = loadedStyles.Find(i => i.GetInfo().name.ToLower() == StyleName.ToLower());
            if (item != null)
            {
                return item.CreateInstance();
            }
            else
            {
                return null;
            }
        }

        [ComVisible(true)]
        string IStyleEngine.Date()
        {
            return LIB_DATE;
        }

        [ComVisible(true)]
        string IStyleEngine.Description()
        {
            return "Provides a mechanism for developers to create redirects using .net.";
        }

        [ComVisible(true)]
        int IStyleEngine.GetConfigWindow(string str)
        {
            if (str.Contains(":"))
            {
                string[] args = str.Split(':');
                // [0] should by the style name; [1] should be the query
                Redirect style = loadedStyles.Find(i => i.StyleName == args[0]);
                if (style != null)
                {
                    WebAdminReply reply = style.DoWebAdmin(_GetQueryParameters(args[1]));
                    this.lastRedirect = reply.RedirectURL;
                    this.lastResponse = reply.ReplyHTML;
                    return reply.StatusCode;
                }
            }
            return 0;
        }

        [ComVisible(true)]
        M_RESULT IStyleEngine.Initialize()
        {
            // load up .net redirects here

            try
            {
                Trace.WriteLine("looking for redirects in {" + this.redirectsPath + "}...");
                foreach (string dir in Directory.EnumerateDirectories(this.redirectsPath))
                {
                    _loadRedirect(dir);
                }

                Trace.WriteLine("done! count = " + loadedStyles.Count);
                if (loadedStyles.Count == 0)
                {
                    Trace.WriteLine("didn't find any redirects");
                    this.lastResponse = "No suitable redirects found";
                    return M_RESULT.M_NOT_FOUND;
                }
                else
                {
                    Trace.WriteLine("init completed");
                    this.lastResponse = "All ok here";
                    return M_RESULT.M_OK;
                }
            }
            catch
            {
                Trace.WriteLine("couldn't access/find search folder");
                this.lastResponse = "Path error accessing redirect store";
                return M_RESULT.M_NO_INTERFACE;
            }
        }

        [ComVisible(true)]
        string IStyleEngine.LastError()
        {
            return this.lastResponse;
        }

        [ComVisible(true)]
        string IStyleEngine.Name()
        {
            return "Net Redirect";
        }

        [ComVisible(true)]
        string IStyleEngine.Path()
        {
            return this.bootPath;
        }

        [ComVisible(true)]
        int IStyleEngine.Revision()
        {
            return LIB_REVISION;
        }

        [ComVisible(true)]
        void IStyleEngine.StyleAt(int Index, ref style_info Style)
        {
            if (Index == -1)
            {
                // don't ask...
                Style.Name = this.lastRedirect;
            }
            else if (Index == 0)
            {
                // return info about the engine
                Style.Name = "NetRedirect";
                Style.IconPath = Path.Combine(this.bootPath, "icon.png");
                Style.Date = LIB_DATE;
                Style.Major = LIB_VERSION;
                Style.Minor = LIB_REVISION;
            }
            else
            {
                Redirect item = loadedStyles[Index - 1];
                if (item != null)
                {
                    RedirectInfo info = item.GetInfo();
                    Style.Copyright = info.vendorCopyright;
                    Style.Date = info.date;
                    Style.Description = info.description;
                    Style.Flags = S_STYLE_FLAGS.S_STYLE_IS_WINDOWLESS | S_STYLE_FLAGS.S_STYLE_V42_CONTENT;

                    if (info.isConfigurable)
                        Style.Flags = Style.Flags | S_STYLE_FLAGS.S_STYLE_IS_CONFIGURABLE;

                    if (info.supportsWebAdmin)
                        Style.Flags = Style.Flags + 0x2000;             // S_STYLE_SUPPORTS_WEB_ADMIN

                    if (info.isWebRedirect)
                        Style.Flags = Style.Flags + 0x8000;             // S_STYLE_WEB_REDIRECT (R4.0)

                    Style.Major = info.versionMajor;
                    Style.Minor = info.versionMinor;
                    Style.Name = info.name;
                    Style.Path = Path.Combine(this.redirectsPath, item.BaseName);
//                    Style.Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
//                                              "full phat\\snarl\\redirects.net", item.baseName);

                    string schemeList = "";
                    foreach (KeyValuePair<string, string> scheme in info.schemes)
                    {
                        schemeList += scheme.Key + "=" + scheme.Value + "|";
                    }

                    Style.Schemes = schemeList.TrimEnd('|');
                    Style.SupportEmail = info.vendorEmail;
                    Style.URL = info.vendorURL;
                    // do after setting .Path!
                    Style.IconPath = Path.Combine(Style.Path, "icon.png");

                }
                else
                {
                    // invalid index
                    Style.Name = "???notfound???";
                }
            }
        }

        [ComVisible(true)]
        void IStyleEngine.TidyUp()
        {
            foreach (Redirect redirect in loadedStyles)
            {
                redirect.Unload();
            }
            loadedStyles.Clear();
        }

        [ComVisible(true)]
        int IStyleEngine.Version()
        {
            return LIB_VERSION;
        }
    }
}
