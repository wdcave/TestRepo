using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Xml.Linq;
using System.IO;

namespace FF.ClientREST
{
  /// <summary>
  /// Do not uri escape parameters as they are converted within this class.
  /// </summary>
  public class REST
  {
    private static string URLPrefix = @"https://www.folderfusion.com/api/1/";
    public static string authCookie = String.Empty;
    private static AutoResetEvent autoEvent = new AutoResetEvent(false);

    public enum UploadIfExists { Overwrite, NewFile, Fail };
    public enum GetInfoLevel { Basic, Detail };

    private string _xmlResponse;
    public string XmlResponse
    {
      get { return _xmlResponse; }
    }

    public REST()
    {
      IgnoreSSLCertificateErrors();        
    }

    /// <summary>
    /// Constructor used for testing.
    /// </summary>
    /// <param name="protocol">Examples "http" and "https"</param>
    /// <param name="domain">Examples "localhost/FolderFusion" and "www.folderfusion.com" </param>
    public REST(string protocol, string domain)
    {
      URLPrefix = protocol + @"://" + domain + @"/api/1/";
      IgnoreSSLCertificateErrors();
    }

    #region Private Methods
    private void ProcessHttpResponse(IAsyncResult iar)
    {
      try
      {
        _xmlResponse = String.Empty; // init in case of failure
        HttpWebRequest request = (HttpWebRequest)iar.AsyncState;
        HttpWebResponse response;
        response = (HttpWebResponse)request.EndGetResponse(iar);
        System.IO.StreamReader strm = new System.IO.StreamReader(response.GetResponseStream());
        _xmlResponse = strm.ReadToEnd();
        response.Close();

        if (null != response.Headers["Set-Cookie"])
          authCookie = response.Headers["Set-Cookie"];
      }
      finally
      {
        //iar.AsyncWaitHandle.Close();
        autoEvent.Set();
      }
    }

    private void IgnoreSSLCertificateErrors()
    {
      ServicePointManager.ServerCertificateValidationCallback +=
        delegate(object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
        {
          return true;
        };
    }
    #endregion

    public int LoginStatus()
    {
      string url = URLPrefix + "loginstatus";

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "Get";
      request.ContentType = "text/xml";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    public int Login(string username, string password)
    {
      if (String.IsNullOrEmpty(username) || (String.IsNullOrEmpty(password)))
        return -1;

      string url = URLPrefix + "login";

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      string contentString = BuildLoginContentString(username, password);
      byte[] buffer = GetBytesFromString(contentString);

      // set type
      request.Method = "POST";
      request.ContentType = "application/x-www-form-urlencoded";
      // add content
      request.ContentLength = buffer.Length;
      Stream newStream = request.GetRequestStream();
      newStream.Write(buffer, 0, buffer.Length);

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    public int CreateAccount(string firstName, string lastName, 
      string username, string password, string phone)
    {
      // validation of required parms
      if (String.IsNullOrEmpty(firstName) || 
          String.IsNullOrEmpty(lastName) || 
          String.IsNullOrEmpty(username) || 
         (String.IsNullOrEmpty(password))
        )
        return -1;

      if (null == phone)
        phone = String.Empty;

      string url = URLPrefix + "createaccount";

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      string contentString = BuildCreateAccountContentString(firstName, lastName,
        username, password, phone);
      byte[] buffer = GetBytesFromString(contentString);

      // set type
      request.Method = "POST";
      request.ContentType = "application/x-www-form-urlencoded";
      // add content
      request.ContentLength = buffer.Length;
      Stream newStream = request.GetRequestStream();
      newStream.Write(buffer, 0, buffer.Length);

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    private string BuildLoginContentString(string username, string password)
    {
      string content = @"<request>" +
                       @"<username>" + username + @"</username>" +
                       @"<password>" + password + @"</password>" +
                       @"</request>";

      return content;
    }

    private string BuildCreateAccountContentString(string firstName, string lastName, 
      string username, string password, string phone)
    {
      string content = @"<request>" +
                       @"<firstname>" + firstName + @"</firstname>" +
                       @"<lastname>" + lastName + @"</lastname>" +
                       @"<username>" + username + @"</username>" +
                       @"<password>" + password + @"</password>" +
                       @"<phone>" + phone + @"</phone>" +
                       @"</request>";

      return content;
    }

    private byte[] GetBytesFromString(string str)
    {
      byte[] bytes = new byte[str.Length * sizeof(char)];
      System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
      return bytes;
    }

    public int DeleteAccount()
    {
      string url = URLPrefix + "deleteaccount";

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "Get";
      request.ContentType = "text/xml";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    public int CreateFolder(string foldername, long parentId, out long newObjectId)
    {
      // init out parms
      newObjectId = -1;

      if (String.IsNullOrEmpty(foldername) || (0 > parentId))
        return -1;

      string url = URLPrefix + "createfolder?parentid=" + parentId.ToString() +
                               "&name=" + Uri.EscapeDataString(foldername);// + "&undelete=" + undelete.ToString();

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "Get";
      request.ContentType = "text/xml";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;

        // get new object id if successful or it already exists (if exists then return existing object id)
        if ((200 == status) || (409 == status))
        {
          var strNewObjectId = XDocument.Parse(XmlResponse).Element("response").Descendants("result").Descendants("objectId").First().Value;

          didParse = long.TryParse(strNewObjectId, out newObjectId);
        }
      }

      return status;
    }

    public int Delete(long id, bool permanent)
    {
      if (id < 0)
        return -1;

      string url = URLPrefix + "delete?id=" + id.ToString()
                             + @"&permanent=" + permanent;

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "Get";
      request.ContentType = "text/xml";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    public int Undelete(long id)
    {
      if (id < 0)
        return -1;

      string url = URLPrefix + "undelete?id=" + id.ToString();

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "Get";
      request.ContentType = "text/xml";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    public int RestoreFolder(long id)
    {
      if (id < 0)
        return -1;

      string url = URLPrefix + "restorefolder?id=" + id.ToString();

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "Get";
      request.ContentType = "text/xml";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Descendants("status").First().Value;

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    public int Upload(long parentId, string filename, UploadIfExists ifExists,
                      bool useRevisioning, long revision, string device,
                      byte[] buffer, int bufferSize, long totalFileSize,
                      out string statusDescription,
                      out string newFilename,
                      out long newFileObjectId,
                      out long newFileRevision)
    {
      // init out parms
      newFilename = null;
      newFileObjectId = -1;
      newFileRevision = -1;
      statusDescription = String.Empty;

      if (parentId < 0)
        return -1;

      string url = URLPrefix + "upload?parentid=" + Uri.EscapeDataString(parentId.ToString())
                             + @"&name=" + Uri.EscapeDataString(filename)
                             + @"&ifexists=" + FF.Common.EnumUtils.StringValueOf(ifExists).ToLower()
                             + @"&userevisioning=" + useRevisioning.ToString().ToLower()
                             + @"&rev=" + revision.ToString().ToLower()
                             + @"&totalfilesize=" + totalFileSize.ToString() // not using range headers due to silverlight
                             + @"&device=" + Uri.EscapeDataString(device);

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "POST";
      request.ContentType = "application/x-www-form-urlencoded";

      // switched to query string parm because of silverlight
      //string rangeValue = String.Format("bytes {0}-{1}/{2}",
      //  0,
      //  (bufferSize > 0) ? bufferSize - 1 : 0,
      //  totalFileSize);
      //request.Headers.Add("Content-Range", rangeValue);
      request.ContentLength = bufferSize;

      Stream newStream = request.GetRequestStream();
      newStream.Write(buffer, 0, bufferSize);

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Element("status").Value;
        string returnFilename = String.Empty;
        string returnId = String.Empty;
        string returnRevision = String.Empty;

        // need to be careful on other tags; for example, a 500 return code might not produce other tags
        try
        {
          returnFilename = XDocument.Parse(XmlResponse).Element("response").Element("filename").Value;
        }
        catch { }
        try
        {
          returnId = XDocument.Parse(XmlResponse).Element("response").Element("id").Value;
        }
        catch { }
        try
        {
          returnRevision = XDocument.Parse(XmlResponse).Element("response").Element("rev").Value;
        }
        catch { }
        try
        {
          statusDescription = XDocument.Parse(XmlResponse).Element("response").Element("statusdescription").Value;
        }
        catch { }

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;

        // get id
        long returnFileObjectIdVal = -1;
        didParse = Int64.TryParse(returnId, out returnFileObjectIdVal);
        if (didParse)
          newFileObjectId = returnFileObjectIdVal;

        // get rev
        long returnRevVal = -1;
        didParse = Int64.TryParse(returnRevision, out returnRevVal);
        if (didParse)
          newFileRevision = returnRevVal;

        // get filename
        newFilename = returnFilename;
      }

      return status;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileId"></param>
    /// <param name="revision"></param>
    /// <param name="offset">0-based</param>
    /// <param name="buffer"></param>
    /// <param name="bufferSize"></param>
    /// <param name="totalFileSize">Last chunk when totalFileSize==offset+bufferSize</param>
    /// <returns></returns>
    public int UploadChunk(long fileId, 
                      long revision,
                      long offset,
                      byte[] buffer, int bufferSize, long totalFileSize,
                      out string statusDescription)
    {
      statusDescription = String.Empty;

      if (fileId < 0)
        return -1;

      if (bufferSize <= 0)
        return -2;

      long rangeend = offset + bufferSize - 1;

      string url = URLPrefix + "uploadchunk?id=" + Uri.EscapeDataString(fileId.ToString())
                             + @"&rangestart=" + offset.ToString()
                             + @"&rangeend=" + rangeend.ToString()
                             + @"&totalfilesize=" + totalFileSize.ToString()
                             + @"&rev=" + revision.ToString().ToLower();

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "POST";
      request.ContentType = "application/x-www-form-urlencoded";

      // switched to query string parm because of silverlight
      //string rangeValue = String.Format("bytes {0}-{1}/{2}",
      //  offset,
      //  offset + bufferSize - 1,
      //  totalFileSize);
      //request.Headers.Add("Content-Range", rangeValue);
      request.ContentLength = bufferSize;

      Stream newStream = request.GetRequestStream();
      newStream.Write(buffer, 0, bufferSize);

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      int status = -1; // way bad on the client api side
      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Element("status").Value;

        // need to be careful on other tags; for example, a 500 return code might not produce other tags
        try
        {
          statusDescription = XDocument.Parse(XmlResponse).Element("response").Element("statusdescription").Value;
        }
        catch { }

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="includeDeleted"></param>
    /// <param name="infoLevel"></param>
    /// <param name="folderInfo"></param>
    /// <returns>-2=Id not found</returns>
    public int GetFolderInfo(long id,
      bool includeDeleted,
      GetInfoLevel infoLevel,
      out FolderInfo folderInfo) 
    {
      int status = -1; // way bad on the client api side

      // init out parms
      folderInfo = new FolderInfo(); // init

      string url = URLPrefix + "getfolderinfo?id=" + Uri.EscapeDataString(id.ToString())
                             + @"&includedeleted=" + includeDeleted.ToString().ToLower()
                             + @"&infolevel=" + FF.Common.EnumUtils.StringValueOf(infoLevel).ToLower();

      HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
      request.ContentLength = 0;

      if (!String.IsNullOrEmpty(authCookie))
        request.Headers["Cookie"] = authCookie;

      // set type
      request.Method = "POST";
      request.ContentType = "application/x-www-form-urlencoded";

      AsyncCallback asyncCallback = new AsyncCallback(ProcessHttpResponse);
      IAsyncResult asyncResult = request.BeginGetResponse(asyncCallback, request);

      autoEvent.WaitOne();

      if (!String.IsNullOrEmpty(XmlResponse))
      {
        // get status
        var returnStatus = XDocument.Parse(XmlResponse).Element("response").Element("status").Value;

        // if object not found, 404 then return status
        if ("404" == returnStatus)
          return 404;

        // need to be careful on other tags; for example, a 500 return code might not produce other tags
        // get folder info
        XElement folderElement = null;
        try
        {
          folderElement = XDocument.Parse(XmlResponse).Element("response").Element("result").Element("folder");
        }
        catch 
        {
          return -2;
        }

        if (null == folderElement)
          return -3;

        try
        {
          if (null != folderElement.Attribute("name"))
            folderInfo.Name = folderElement.Attribute("name").Value;
        } catch { }

        try
        {
          long temp;
          if (null != folderElement.Attribute("objectid"))
            if (Int64.TryParse(folderElement.Attribute("objectid").Value, out temp))
              folderInfo.ObjectId = temp;
        } catch { }

        try
        {
          bool temp;
          if (null != folderElement.Attribute("isdeleted"))
            if (Boolean.TryParse(folderElement.Attribute("isdeleted").Value, out temp))
              folderInfo.IsDeleted = temp;
        } catch { }

        try
        {
          long temp;
          if (null != folderElement.Attribute("parentid"))
            if (Int64.TryParse(folderElement.Attribute("parentid").Value, out temp))
              folderInfo.ParentId = temp;
        } catch { }

        try
        {
          if (null != folderElement.Attribute("version"))
            folderInfo.Version = folderElement.Attribute("version").Value;
        } catch { }

        try
        {
          DateTime temp;
          if (null != folderElement.Attribute("lastupdated")) 
            if (DateTime.TryParse(folderElement.Attribute("lastupdated").Value, out temp))
              folderInfo.LastUpdated = temp;
        } catch { }

        // other folder info
        try
        {
          if (null != folderElement.Attribute("description"))
            folderInfo.Description = folderElement.Attribute("description").Value;
        }
        catch { }

        try
        {
          bool temp;
          if (null != folderElement.Attribute("indexed"))
            if (Boolean.TryParse(folderElement.Attribute("indexed").Value, out temp))
              folderInfo.Indexed = temp;
        }
        catch { }
        
        try
        {
          if (null != folderElement.Attribute("createdby"))
            folderInfo.CreatedBy = folderElement.Attribute("createdby").Value;
        }
        catch { }

        try
        {
          DateTime temp;
          if (null != folderElement.Attribute("createdtime"))
            if (DateTime.TryParse(folderElement.Attribute("createdtime").Value, out temp))
              folderInfo.CreatedTime = temp;
        }
        catch { }
        
        try
        {
          if (null != folderElement.Attribute("deletedby"))
            folderInfo.DeletedBy = folderElement.Attribute("deletedby").Value;
        }
        catch { }
     
        try
        {
          DateTime temp;
          if (null != folderElement.Attribute("deletedtime"))
            if (DateTime.TryParse(folderElement.Attribute("deletedtime").Value, out temp))
              folderInfo.DeletedTime = temp;
        } catch { }
     
        try
        {
          int temp;
          if (null != folderElement.Attribute("state"))
            if (Int32.TryParse(folderElement.Attribute("state").Value, out temp))
              folderInfo.State = temp;
        } catch { }

        // get subfolders
        try
        {
          foreach (XElement xe in folderElement.Element("folders").Descendants("folder"))
          {
            bool error = false;
            FolderInfo subFolder = new FolderInfo();

            try
            {
              if (null != xe.Attribute("name"))
                subFolder.Name = xe.Attribute("name").Value;
            }
            catch { error = true; }
            
            try
            {
              long temp;
              if (null != xe.Attribute("objectid"))
                if (Int64.TryParse(xe.Attribute("objectid").Value, out temp))
                  subFolder.ObjectId = temp;
            }
            catch { error = true;}

            try
            {
              bool temp;
              if (null != xe.Attribute("isdeleted"))
                if (Boolean.TryParse(xe.Attribute("isdeleted").Value, out temp))
                  subFolder.IsDeleted = temp;
            } catch { error = true;}

            try
            {
              if (null != xe.Attribute("version"))
                subFolder.Version = xe.Attribute("version").Value;
            } catch { error = true;}
                        
            try
            {
              DateTime temp;
              if (null != xe.Attribute("lastupdated"))
                if (DateTime.TryParse(xe.Attribute("lastupdated").Value, out temp))
                  subFolder.LastUpdated = temp;
            }
            catch { error = true; }

            if (false == error)
            {
              folderInfo.subFolders.Add(subFolder);
            }
          }
        }
        catch { }

        // get subfiles
        try
        {
          foreach (XElement xe in folderElement.Element("files").Descendants("file"))
          {
            bool error = false;
            FF.ClientREST.FileInfoBasic subFile = new FF.ClientREST.FileInfoBasic();

            try
            {
              if (null != xe.Attribute("name"))
                subFile.Name = xe.Attribute("name").Value;
            }
            catch { error = true; }

            try
            {
              long temp;
              if (null != xe.Attribute("objectid"))
                if (Int64.TryParse(xe.Attribute("objectid").Value, out temp))
                  subFile.ObjectId = temp;
            }
            catch { error = true; }

            try
            {
              if (null != xe.Attribute("checksum"))
                subFile.Checksum = xe.Attribute("checksum").Value;
            }
            catch { error = true; }

            try
            {
              bool temp;
              if (null != xe.Attribute("isdeleted"))
                if (Boolean.TryParse(xe.Attribute("isdeleted").Value, out temp))
                  subFile.IsDeleted = temp;
            }
            catch { error = true; }

            try
            {
              DateTime temp;
              if (null != xe.Attribute("createdtime"))
                if (DateTime.TryParse(xe.Attribute("createdtime").Value, out temp))
                  subFile.CreatedTime = temp;
            }
            catch { error = true; }

            try
            {
              long temp;
              if (null != xe.Attribute("revision"))
                if (Int64.TryParse(xe.Attribute("revision").Value, out temp))
                  subFile.FileRevision = temp;
            }
            catch { error = true; }

            try
            {
              long temp;
              if (null != xe.Attribute("sizeinbytes"))
                if (Int64.TryParse(xe.Attribute("sizeinbytes").Value, out temp))
                  subFile.SizeInBytes = temp;
            }
            catch { error = true; }
           
            try
            {
              int temp;
              if (null != xe.Attribute("state"))
                if (Int32.TryParse(xe.Attribute("state").Value, out temp))
                  subFile.State = temp;
            }
            catch { }

            try
            {
              if (null != xe.Attribute("version"))
                subFile.Version = xe.Attribute("version").Value;
            }
            catch { }

            // set parent, it's obvious
            subFile.ParentId = folderInfo.ObjectId;

            if (false == error)
            {
              folderInfo.subFiles.Add(subFile);
            }
          }
        }
        catch { }

        int returnIntVal;
        bool didParse = Int32.TryParse(returnStatus, out returnIntVal);
        if (didParse)
          status = returnIntVal;
      }

      return status;
    }
  }

  public class FileInfoBasic
  {
    public string Name { get; set; }
    public long ObjectId { get; set; }
    public long ParentId { get; set; }
    public long FileRevision { get; set; }
    public bool? IsDeleted { get; set; }
    public string Version { get; set; }
    public long SizeInBytes { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateTime? CreatedTime { get; set; }
    public string Checksum { get; set; }
    public int State { get; set; }

    public FileInfoBasic()
    {
    }
  }

  public class FolderInfo
  {
    public string Name { get; set; }
    public long ObjectId { get; set; }
    public long? ParentId { get; set; }
    public string Tags { get; set; }
    public string Description { get; set; }
    public bool? Indexed { get; set; }
    public DateTime? CreatedTime { get; set; }
    public DateTime? DeletedTime { get; set; }
    public bool? IsDeleted { get; set; }
    public string Version { get; set; }
    public DateTime? LastUpdated { get; set; }
    public int State { get; set; }
    public long? SizeInBytes { get; set; }
    public string CreatedBy { get; set; }
    public string DeletedBy { get; set; }

    public List<FolderInfo> subFolders;
    public List<FileInfoBasic> subFiles;

    public FolderInfo()
    {
      Name = null; // invalid
      ObjectId = -1; // invalid
      ParentId = null;
      Tags = null;
      Description = null;
      Indexed = null;
      CreatedTime = null;
      IsDeleted = null;
      Version = null;
      LastUpdated = null;
      State = -1; // invalid
      SizeInBytes = null;

      subFolders = new List<FolderInfo>();
      subFiles = new List<FileInfoBasic>();
    }
  }
}
