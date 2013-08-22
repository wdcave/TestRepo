using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FF.ClientREST;
using System.Net;
using System.Threading;

namespace FFDesktop1
{
  public partial class Form1 : Form
  {
    public FF.ClientREST.REST rest;
    Queue<QueueItem> downloadQueue = new Queue<QueueItem>();

    public Form1()
    {
      InitializeComponent();
    }

    private void buttonGo_Click(object sender, EventArgs e)
    {
      string targetPath = textBox1.Text;

      // create directory if no exist
      try
      {
        System.IO.Directory.CreateDirectory(targetPath);
        Report("Target path verified");
      }
      catch
      {
        Report("Error with target path");
      }

      // login
      rest = new REST("http", "www.digitous.com");
      int status = rest.Login("dcave", "superman1!");
      if (200 == status)
        Report("Login successful");
      else
      {
        Report("Login failed!");
        return;
      }

      string destPathForRoot = targetPath;
      DownloadFolder(0, destPathForRoot);

      // start worker
      BackgroundWorker bgw = new BackgroundWorker();
      bgw.WorkerReportsProgress = true;
      bgw.DoWork += new DoWorkEventHandler(bgw_DoWork);
      bgw.ProgressChanged += new ProgressChangedEventHandler(bgw_ProgressChanged);
      bgw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgw_RunWorkerCompleted);

      bgw.RunWorkerAsync(null);
    }

    private void DownloadFolder(long folderId, string destPath)
    {
      // get root folder info
      FolderInfo folderInfo = null;
      string targetPath = destPath;

      int status = rest.GetFolderInfo(folderId, true, REST.GetInfoLevel.Basic, out folderInfo);
      if (200 == status)
        Report("Successfully retrieved folder info");
      else
        Report("Failed to get folder info");

      if (0 == folderId)
      {
        // special case root
        System.IO.Directory.CreateDirectory(targetPath);
        Report("Created folder " + targetPath);
      }
      else
      {
        System.IO.Directory.CreateDirectory(targetPath = System.IO.Path.Combine(destPath, folderInfo.Name));
        Report("Created folder " + folderInfo.Name);
      }

      //bool lockWasTaken = false;
      foreach (FileInfoBasic cloudFile in folderInfo.subFiles)
      {
        //try
        //{
        string fullPath = System.IO.Path.Combine(targetPath, cloudFile.Name);
        QueueItem queueItem = new QueueItem(cloudFile.ObjectId, cloudFile.FileRevision, fullPath);
        //  Monitor.Enter(downloadQueue);// ref lockWasTaken);
        //  // do queue stuff
        //  downloadQueue.Enqueue(queueItem);
        //}
        //finally
        //{
        //  if (lockWasTaken)
        //    Monitor.Exit(downloadQueue);
        //}
        lock (downloadQueue)
        {
          downloadQueue.Enqueue(queueItem);
        }
      }

      foreach (FolderInfo cloudFolder in folderInfo.subFolders)
      {
       // string fullPath = System.IO.Path.Combine(
        DownloadFolder(cloudFolder.ObjectId, targetPath);
      }
    }

    void bgw_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
      //Remove from listbox. e.UserState is the value supplied in DoWork event
      listBox1.Items.Remove((string)e.UserState);
    }

    void bgw_DoWork(object sender, DoWorkEventArgs e)
    {
      //bool lockWasTaken = false;

      //Uploading files
      //foreach (string file in (string[])e.Argument)
      //{
      //  //Simulate upload file for each file
      //  System.Threading.Thread.Sleep(500);
      //  ((BackgroundWorker)sender).ReportProgress(0, file);//which file reported to progressChanged
      //}

      while (true)
      {
        QueueItem queueItem = null;

        //try
        //{
        //  Monitor.Enter(downloadQueue, ref lockWasTaken);
        //  // do queue stuff
        //  queueItem = downloadQueue.Dequeue();
        //}
        //finally
        //{
        //  if (lockWasTaken)
        //    Monitor.Exit(downloadQueue);
        //}
        lock (downloadQueue)
        {
          if (downloadQueue.Count > 0)
            queueItem = downloadQueue.Dequeue();
        }

        if (null != queueItem)
        {
          string url = "http://www.digitous.com/handlers/downloadhandler.ashx?oid=" + queueItem._fileId.ToString() +
            @"&rev=" + queueItem._fileRev.ToString();

          WebClient webClient = new WebClient();
          webClient.Headers["Cookie"] = REST.authCookie;
          webClient.DownloadFile(url, queueItem._destPath);

          if (listBox1.InvokeRequired)
          {
            this.Invoke((Action)(() => listBox1.Items.Add("File Downloaded: " + queueItem._destPath)));
          }
          else
          {
            listBox1.Items.Add("File Downloaded: " + queueItem._destPath);
          }
        }

        Thread.Sleep(1000);
      }
    }

    void bgw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      //Release resources consumed by the BackgroundWorker
      ((IDisposable)sender).Dispose();
    }

    private void Report(string s)
    {
      listBox1.Items.Add(s);
    }

    private void DownloadFile(string file)
    {
      WebClient webClient = new WebClient();
      webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);
      webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
      webClient.DownloadFileAsync(new Uri("http://mysite.com/myfile.txt"), @"c:\myfile.txt");
    }

    private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
      //progressBar.Value = e.ProgressPercentage;
    }

    private void Completed(object sender, AsyncCompletedEventArgs e)
    {
      MessageBox.Show("Download completed!");
    }
  }

  public class QueueItem
  {
    public long _fileId;
    public long _fileRev;
    public string _destPath;

    public QueueItem(long fileId, long fileRev, string destPath)
    {
      _fileId = fileId;
      _fileRev = fileRev;
      _destPath = destPath;
    }
  }
}
