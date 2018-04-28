/*
 * 
  Author:   Richard O'Dwyer
  License:  WTFPL, Version 2 (http://www.gnu.org/licenses/license-list.html#WTFPL)
  Version:  1.1
  E-mail:   richard@richard.do
  Url:      https://github.com/richardasaurus
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Threading;

namespace MovieTitleTidy
{
    public partial class MainForm : Form
    {
        bool runIt = false;
        string folderPath = "";
        Thread tidyThread;

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                folderPath = folderBrowserDialog1.SelectedPath;
                if (folderPath != null)
                {
                    runIt = true;
                    LiveLog("Selected folder: " + folderPath);
                    btnStartRename.Visible = true;
                }
            }

        }

        public void ProcessFolders(string fileSource)
        {
            LiveLog("Started");

            string[] dirs = System.IO.Directory.GetDirectories(fileSource);

            LiveLog("Found " + dirs.Length + " sub directories");

            foreach (string dir in dirs)
            {
                ProcessDirectory(dir);
            }
        }


        public void ProcessDirectory(string fileSource)
        {
            string[] typesArray = new string[] { ".avi", ".mp4", ".mov", ".mkv", ".wmv", ".mpg", "mpeg", ".rm", ".flv" };
            string[] badWords = new string[] { "sample" };
            string[] fileEntries = Directory.GetFiles(fileSource);


            foreach (string fileNamePath in fileEntries)
            {
                // do something with fileName
                string path = Path.GetDirectoryName(fileNamePath);
                string fileName = Path.GetFileName(fileNamePath).Trim();
                string fileExt = Path.GetExtension(fileName);
                string title = fileName.Replace(fileExt, "");

                if (typesArray.Contains(fileExt) == true && fileName.ToLower().Contains(badWords[0]) == false) //found video file in folder
                {
                    string requestTitle = ProcessTitle(title);

                    if (requestTitle.Trim().Length > 2)
                    {
                        //grab imdb title
                        string imdb_resp = GetImdbTitle(requestTitle).Trim();
                        string cleanedFilename = "";

                        if (imdb_resp != "Error")
                        {

                            if (imdb_resp != "Not found")
                            {
                                //if imdb failed, use backup title
                                cleanedFilename = imdb_resp + fileExt;
                            }
                            else
                            {
                                // Changes a string to titlecase.
                                TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                                requestTitle = textInfo.ToTitleCase(requestTitle);

                                //set final filename
                                cleanedFilename = requestTitle + fileExt;
                            }

                            //rename the file
                            string oldfile = fileSource + "\\" + fileName;
                            string newfile = path + "\\" + cleanedFilename;
                            LiveLog("Renaming " + oldfile);
                            LiveLog("To " + newfile);
                            if (oldfile != newfile)
                            {

                                if (!File.Exists(newfile))
                                    System.IO.File.Move(oldfile, newfile);
                            }

                        }
                        else
                        {
                            //http error
                            break;
                        }
                    }
                }
                else
                {
                    //LiveLog("No video files found, try another folder perhaps.");
                }

            } // foreach file in dir

        } //ProcessDir

        public static string ProcessTitle(string title)
        {
            title = title.ToLower();

            //remove common unwanted symbols
            title = title.Replace(".", " ")
            .Replace("-", " ")
            .Replace("[", " ")
            .Replace("]", " ")
            .Replace("_", " ");

            //bad words to remove from source title
            string[] commonWords = new string[] {
                "axxo","dvdrip","brrip","hdrip","720p","1080p","x264"," eng ",
                "750mb","350mb"," ts ","xvid","dual audio", "hd rip","br rip",
                "dvd rip"," subs "," limited "," extended ","ac3","dvdscreener",
                "ts scr","new source","publichd","hardsubs","hindi","mkv"," hq ",
                " nl "," dts "," ger "," pal "," ntsc ","dd5.1","512x384","25fps",
                "921kbs","multisub","collectors","edition","512x288", "24fps", "674kbs","v7mp3"
            };

            foreach (string word in commonWords)
            {
                if (title.Contains(word))
                {
                    title = title.Replace(word, "");

                    //remove everything after dbl space
                    int index = title.IndexOf("  ");
                    if (index > 0)
                        title = title.Substring(0, index);
                    title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ");
                }
            }

            return title;

        } //ProcessTitle

        public static string GetImdbTitle(string title)
        {
            //look for a date in source title
            Regex datePattern = new Regex("(19|20)[0-9][0-9]");
            Match dateMatch = datePattern.Match(title);
            string date = dateMatch.Groups[0].ToString();

            string url = "http://www.deanclatworthy.com/imdb/?q=";

            if (dateMatch.Success)
            {
                url += title.Replace(date, "").Trim() + "&year=" + dateMatch.Groups[0].ToString();
            }
            else
            {
                url += title.Trim();
            }

            url = url.Replace(" ", "%20").Replace("(", "").Replace(")", "");
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            //Convert the json responce to final title
            try
            {
                using (HttpWebResponse HttpResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    var streamReader = new StreamReader(HttpResponse.GetResponseStream());
                    var responseText = streamReader.ReadToEnd();

                    //remove api error garbage
                    string apiError = "{\"code\":2,\"error\":\"Exceeded API usage limit\"}";
                    responseText = responseText.Replace(apiError, "");

                    JObject o = JObject.Parse(responseText);

                    if ((string)o["error"] == null)
                    {
                        //set title and year to imdb's
                        string imdb_title = (string)o["title"];
                        string imdb_year = (string)o["year"];

                        if (imdb_year != null)
                        {
                            int year_len = imdb_year.Length;

                            if (year_len == 4)
                            {
                                imdb_title = imdb_title + " (" + imdb_year + ")";
                            }
                        }

                        imdb_title = System.Net.WebUtility.HtmlDecode(Uri.UnescapeDataString(imdb_title)).Replace(":", "").Trim();
                        return imdb_title;
                    }
                    else
                    {
                        return "Not found";
                    }

                } //using http


            }
            catch (WebException e)
            {
                return "Error";
            }

        } //GetImdbTitle

        public MainForm()
        {
            InitializeComponent();
        }

        public void LiveLog(string output)
        {
            this.BeginInvoke(new MethodInvoker(delegate()
            {
            lstOutput.Items.Add(output);
            }));

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LiveLog("Select a folder to format video filenames in...");
        }


        private void Start()
        {
            this.BeginInvoke(new MethodInvoker(delegate()
            {
                lstOutput.Items.Clear();
            }));

            ProcessFolders(folderPath);
            LiveLog("Renaming complete!");
            System.Windows.Forms.MessageBox.Show("Renaming complete!");

            this.BeginInvoke(new MethodInvoker(delegate()
            {
                btnStartRename.Visible = false;
            }));
            runIt = false;
        }

        private void btnStartRename_Click(object sender, EventArgs e)
        {
            if (runIt == true)
            {
                tidyThread = new Thread(new ThreadStart(Start));
                tidyThread.Start();
            }
        }

    } //class
} //namespace
