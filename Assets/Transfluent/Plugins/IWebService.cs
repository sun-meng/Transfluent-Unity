﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Pathfinding.Serialization.JsonFx;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace transfluent
{
	public interface IWebService
	{
		WebServiceReturnStatus request(string url);
		WebServiceReturnStatus request(string url, Dictionary<string, string> postParams);
		WebServiceReturnStatus request(ITransfluentParameters parameters);
		string encodeGETParams(Dictionary<string, string> getParams);
	}

	public class DebugSyncronousEditorWebRequest : IWebService
	{
		private readonly IWebService realRequest = new SyncronousEditorWebRequest();
		public bool debug = true;

		public DebugSyncronousEditorWebRequest()
		{
			if(debug) Debug.Log("CREATING SYNC REQUESTs");
		}

		public WebServiceReturnStatus request(string url)
		{
			if(debug) Debug.Log("calling url:" + url + "(GET) ");
			WebServiceReturnStatus result = realRequest.request(url);
			if(debug) Debug.Log("GOT BACK WITH RESULT:" + result);
			return result;
		}

		public WebServiceReturnStatus request(string url, Dictionary<string, string> postParams)
		{
			if (postParams != null)
			{
				foreach (var param in postParams)
				{
					if(debug) Debug.Log("Field added:" + param.Key + " with value:" + param.Value);
				}
				if(debug) Debug.Log("ALL params:" + JsonWriter.Serialize(postParams));
			}
			if(debug) Debug.Log("calling url:" + url + "(POST) ");
			WebServiceReturnStatus result = realRequest.request(url, postParams);

			if(debug) Debug.Log("GOT BACK WITH RESULT:" + result);
			return result;
		}

		public WebServiceReturnStatus request(ITransfluentParameters call)
		{
			return realRequest.request(call);
		}

		public string encodeGETParams(Dictionary<string, string> getParams)
		{
			return realRequest.encodeGETParams(getParams);
		}
	}

	public class SyncronousEditorWebRequest : IWebService
	{
		public WebServiceReturnStatus request(string url)
		{
			return doWWWCall(new WWW(url));
		}

		public WebServiceReturnStatus request(string url, Dictionary<string, string> postParams)
		{
			var form = new WWWForm();
			if (postParams != null)
			{
				foreach (var param in postParams)
				{
					if (param.Value == null)
					{
						throw new Exception("NULL PARAMATER PASSED TO WEB REQUEST:" + param.Key);
					}

					form.AddField(param.Key, param.Value);
				}
			}

			return doWWWCall(new WWW(url, form));
		}

		public WebServiceReturnStatus request(ITransfluentParameters call)
		{
			Route route = RestUrl.GetRouteAttribute(call);
			string url = RestUrl.GetURL(call);
			WebServiceReturnStatus status;
			string urlWithGetParams = url + encodeGETParams(call.getParameters);
			if (route.requestType == RestRequestType.GET)
			{
				status = request(urlWithGetParams);
			}
			else
			{
				status = request(urlWithGetParams, call.postParameters);
			}

			return status;
		}

		public string encodeGETParams(Dictionary<string, string> getParams)
		{
			var sb = new StringBuilder();
			sb.Append("?");
			foreach (var kvp in getParams)
			{
				sb.Append(WWW.EscapeURL(kvp.Key) + "=" + WWW.EscapeURL(kvp.Value) + "&");
			}
			string fullUrl = sb.ToString();
			if (fullUrl.EndsWith("&"))
			{
				fullUrl = fullUrl.Substring(0, fullUrl.LastIndexOf("&"));
			}
			return fullUrl;
		}

		private WebServiceReturnStatus doWWWCall(WWW www)
		{
			var status = new WebServiceReturnStatus();

			var sw = new Stopwatch();
			sw.Start();
			while (true)
			{
				if (www.isDone)
					break;
				if (www.error != null)
					break;
				if (sw.Elapsed.TotalSeconds >= 30f)
					break;
					
				//EditorApplication.Step();
				Thread.Sleep(100);
			}

			sw.Stop();
			
			status.requestTimeTaken = sw.Elapsed;

			if (!www.isDone && www.error == null)
			{
				throw new TransportException("Timeout total time taken:"+sw.Elapsed);
			}
			if (www.error == null)
			{
				status.text = www.text;
			}
			else
			{
				string error = www.error;
				if (knownTransportError(error)) 
				{
					www.Dispose();
					throw new TransportException(error);
				}
				status.httpErrorCode = -1;
				int firstSpaceIndex = error.IndexOf(" ");
				 
				if (firstSpaceIndex > 0)
				{
					www.Dispose();

					int.TryParse(error.Substring(0, firstSpaceIndex), out status.httpErrorCode);
						//there has to be a better way to get error codes
					if (status.httpErrorCode == 0)
					{
						throw new Exception("UNHANDLED ERROR CODE FORMAT:(" + error + ")");
					}
					if (status.httpErrorCode >= 400 && status.httpErrorCode <= 499)
					{
						throw new ApplicatonLevelException("HTTP Error code, applicatin level:" + status.httpErrorCode,status.httpErrorCode);
					}
					throw new HttpErrorCode(status.httpErrorCode);
				}
				throw new Exception("Unknown error:" + error); //can't parse error status
			}
			www.Dispose();
			return status;
		}

		public bool knownTransportError(string input)
		{
			if (input.Contains("Could not resolve host"))
			{
				return true;
			}
			return false;
		}

		//Could not resolve host: transfluent.com (Could not contact DNS servers)
	}

	public struct WebServiceReturnStatus
	{
		public int httpErrorCode;
		public TimeSpan requestTimeTaken;

		public string text; //if text is the  requested thing

		public override string ToString()
		{
			return "RETURN STATUS:" + JsonWriter.Serialize(this) + " time in seconds taken:" + requestTimeTaken.TotalSeconds;
		}
	}
}