using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using LitJson;

namespace genglsljs
{
    public partial class Form1 : Form
    {
        private readonly string configFile = "config.txt";
        private string rootFolder;

        public Form1()
        {
            InitializeComponent();

            GetLastFolderSelected();

            toolStripStatusLabel1.Text = "等待任务中";
        }

        private void selectFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            DialogResult result = fbd.ShowDialog();
            if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                textBox1.Text = fbd.SelectedPath;

                //save last folder selected
                SaveLastFolderSelected(fbd.SelectedPath);
            }
        }

        private void SaveLastFolderSelected(string path)
        {
            try
            {
                if (File.Exists(configFile))
                {
                    string content = File.ReadAllText(configFile);
                    JsonData jsonData = JsonMapper.ToObject(content);
                    jsonData["lastFolder"] = path;
                    string json = JsonMapper.ToJson(jsonData);
                    File.WriteAllText(configFile, json);
                }
                else
                {
                    var fs = File.Create(configFile);
                    fs.Close();

                    path = path.Replace('\\', '/');
                    string json = "{\"lastFolder\": ";
                    json += "\"";
                    json += path;
                    json += "\"";
                    json += "}";

                    File.WriteAllText(configFile, json);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private string GetLastFolderSelected()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    string content = File.ReadAllText(configFile);
                    JsonData jsonData = JsonMapper.ToObject(content);
                    string lastFolder = (string)jsonData["lastFolder"];
                    textBox1.Text = lastFolder;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
                return string.Empty;
            }

            return string.Empty;
        }

        private void generate_Click(object sender, EventArgs e)
        {
            string folder = textBox1.Text;

            FileAttributes attr = File.GetAttributes(folder);

            if (!attr.HasFlag(FileAttributes.Directory))
            {
                MessageBox.Show("请选择一个包含glsl文件的文件夹。");
                return;
            }

            rootFolder = folder.Replace("\\", "/");
            ProcessFiles(folder);

            toolStripStatusLabel1.Text = "处理完毕";
        }

        private void ProcessFiles(string folder)
        {
            List<string> collections = new List<string>();

            // files
            string[] files = Directory.GetFiles(folder);
            for (int i = 0; i < files.Length; ++i)
            {
                string extension = Path.GetExtension(files[i]);

                if (!extension.Equals(".glsl")) continue;

                string folderPath = Path.GetDirectoryName(files[i]);
                string fileName = Path.GetFileNameWithoutExtension(files[i]);
                string outputFilePath = Path.Combine(folderPath, fileName).Replace("\\", "/");

                // collection for shaderCollection.js
                string nameBaseOnRoot = outputFilePath.Substring(rootFolder.Length, outputFilePath.Length - rootFolder.Length);
                string collectName;
                if (!nameBaseOnRoot.Substring(0, 1).Equals("/"))
                    collectName = "./" + nameBaseOnRoot;
                else
                    collectName = "." + nameBaseOnRoot;
                collections.Add(collectName);

                // javascript file
                outputFilePath += ".js";

                string[] lines = File.ReadAllLines(files[i]);
                StreamWriter sw = File.CreateText(outputFilePath);
                
                //添加的头
                sw.WriteLine("define(function() {");
                sw.WriteLine("    'use strict';");
                sw.WriteLine();

                //内容的第一行
                sw.WriteLine("return \"" + lines[0] + @"\n\");

                for (int line = 1; line < lines.Length - 1; ++line)
                {
                    sw.WriteLine(lines[line] + @"\n\");
                }

                //内容的最后一行
                sw.WriteLine(lines[lines.Length-1] + "\";");

                //添加的尾
                sw.WriteLine("});");

                sw.Flush();
                sw.Close();
              }

            // folders
            string[] folders = Directory.GetDirectories(folder);
            for (int i = 0; i < folders.Length; ++i)
            {
                ProcessFiles(folders[i]);
            }

            GenerateShaderCollection(collections);
        }

        private void GenerateShaderCollection(List<string> collections)
        {
            // create shaderCollection.js
            StreamWriter shaderCollectionFile = File.CreateText(Path.Combine(rootFolder, "shaderCollection.js"));

            shaderCollectionFile.WriteLine("define([");

            for (int i = 0; i < collections.Count; ++i)
            {
                shaderCollectionFile.WriteLine("\t'" + collections[i] + "',");
            }

            shaderCollectionFile.WriteLine("\t], function(");

            for (int i = 0; i < collections.Count; ++i)
            {
                int pos = collections[i].LastIndexOf('/');
                string name = collections[i].Substring(pos + 1, collections[i].Length - pos - 1);
                shaderCollectionFile.WriteLine("\t" + name + ",");
            }

            shaderCollectionFile.WriteLine("\t){");
            shaderCollectionFile.WriteLine();

            shaderCollectionFile.WriteLine("\tvar collection = [];");
            shaderCollectionFile.WriteLine();

            for (int i = 0; i < collections.Count; ++i)
            {
                int pos = collections[i].LastIndexOf('/');
                string name = collections[i].Substring(pos + 1, collections[i].Length - pos - 1);

                string line = "collection['";
                line += name;
                line += "'] = ";
                line += name;
                line += ";";

                shaderCollectionFile.WriteLine("\t" + line);
            }

            shaderCollectionFile.WriteLine();

            shaderCollectionFile.WriteLine("\treturn collection;");
            shaderCollectionFile.WriteLine("});");

            shaderCollectionFile.Flush();
            shaderCollectionFile.Close();
        }

    }
}
