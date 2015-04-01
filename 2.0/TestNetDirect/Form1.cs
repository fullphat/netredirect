using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Xml;
using libSnarlStyles;

namespace TestNetDirect
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private XmlNode _createElement(XmlDocument source, string name, string innerText)
        {
            XmlNode aNode = source.CreateElement(name);
            aNode.InnerText = innerText;
            return aNode;
        }

        private void button1_Click(object sender, EventArgs e)
        {

            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "full phat\\snarl\\redirects.net", "boxcar", "boxcar.conf");

            XmlDocument configFile = new XmlDocument();

            try
            {
                configFile.Load(configPath);
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

                //for (int i = 0; i < 4; i++)
                //{
                //    w.WriteStartElement("scheme");
                //    w.WriteElementString("api_key", "1a2b3c4d5e6f");
                //    w.WriteEndElement();
                //}

                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndDocument();
                w.Close();

                configFile.Load(configPath);

            }


            XmlNode newNode = configFile.CreateElement("scheme");
            newNode.AppendChild(_createElement(configFile, "guid", Guid.NewGuid().ToString()));
            newNode.AppendChild(_createElement(configFile, "name", "New scheme"));
            newNode.AppendChild(_createElement(configFile, "api_key", "{enter your api key here}"));

            XmlNode root = configFile.SelectSingleNode("config");
            root = root.SelectSingleNode("schemes");
            root.AppendChild(newNode);

            configFile.Save(configPath);
    
    

            //IStyleEngine engine = new netredirect.StyleEngine();
            //engine.Initialize();

            //int c = engine.CountStyles();
            //style_info info = new style_info();

            //for (int i = 1; i <= c; i++)
            //{
            //    engine.StyleAt(i, info);
            //    Debug.WriteLine(">" + info.Name);
            //}

        }
    }
}
