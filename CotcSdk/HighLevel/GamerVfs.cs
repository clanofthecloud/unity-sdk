﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace CotcSdk {

	/// @ingroup gamer_classes
	/// <summary>
	/// Represents a key/value system, also known as virtual file system.
	/// This class is scoped by domain, meaning that you can call .Domain("yourdomain") and perform
	/// additional calls that are scoped.
	/// </summary>
	public sealed class GamerVfs {

        private static string s3ContentType = null;

        static GamerVfs()
        {
            RuntimePlatform platform = Application.platform;
            string version = Application.unityVersion;

            // "Nice" system to send the correct Content-Type to S3 for upload, since Unity uses different ones
            // based on the platform and version. We must make sure to send the same one since the signature
            // uses the ContentType.
            if(platform == RuntimePlatform.Android)
                s3ContentType = "application/x-www-form-urlencoded";
            if (version.Contains("201") || (version.CompareTo("5.1.1") == 1))
                s3ContentType = "application/octet-stream";
        }

        /// <summary>
        /// Sets the domain affected by this object.
        /// You should typically use it this way: `gamer.GamerVfs.Domain("private").SetKey(...);`
        /// </summary>
        /// <param name="domain">Domain on which to scope the VFS. Defaults to `private` if not specified.</param>
        /// <returns>This object for operation chaining.</returns>
        public GamerVfs Domain(string domain) {
			this.domain = domain;
			return this;
		}

        /// <summary>Retrieves an individual key from the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed. The attached bundle contains the fetched
        ///     property. As usual with bundles, it can be casted to the proper type you are expecting.
        ///     If the property doesn't exist, the call is marked as failed with a 404 status.</returns>
        /// <param name="key">The name of the key to be fetched.</param>
        /// <remarks>This method is obsolete, use GetValue instead.</remarks>
        [Obsolete("Will be removed soon. Use GetValue instead.")]
        public Promise<Bundle> GetKey(string key) {
			UrlBuilder url = new UrlBuilder("/v1/gamer/vfs").Path(domain).Path(key);
			HttpRequest req = Gamer.MakeHttpRequest(url);
			return Common.RunInTask<Bundle>(req, (response, task) => {
                // For backward compatibilty of json input in the backoffice as litterals
				if (response.BodyJson.Has("value"))
                    task.PostResult(response.BodyJson["value"]);
                else
                    task.PostResult(response.BodyJson);
            });
		}

        /// <summary>Retrieves an individual key or all keys from the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed. The attached bundle contains the fetched
        ///     property(ies) in the "result" key. As usual with bundles, it can be casted to the proper type you
        ///     are expecting. If the property doesn't exist, the call is marked as failed with a 404 status.</returns>
        /// <param name="key">The name of the key to be fetched. If you don't pass any key, then all the keys
        ///     will be returned in a global JSON</param>
        public Promise<Bundle> GetValue(string key=null)
        {
            UrlBuilder url = new UrlBuilder("/v3.0/gamer/vfs").Path(domain);
            if(key != null && key != "")
                url = url.Path(key);
            HttpRequest req = Gamer.MakeHttpRequest(url);
            return Common.RunInTask<Bundle>(req, (response, task) => {
                task.PostResult(response.BodyJson);
            });
        }

        /// <summary>Retrieves the binary data of an individual key from the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed. The binary data is attached as the value
        ///     of the result. Please ensure that the key was set with binary data before, or this call will
        ///     fail with a network error.</returns>
        /// <param name="key">The name of the key to be fetched.</param>
        /// <remarks>This method is obsolete, use GetBinary instead.</remarks>
        [Obsolete("Will be removed soon. Use GetBinary instead.")]
        public Promise<byte[]> GetKeyBinary(string key) {
			UrlBuilder url = new UrlBuilder("/v1/gamer/vfs").Path(domain).Path(key).QueryParam("binary");
			HttpRequest req = Gamer.MakeHttpRequest(url);
			return Common.RunInTask<byte[]>(req, (response, task) => {
                // We must then download the received URL
				string downloadUrl = response.BodyString.Trim('"');
				HttpRequest binaryRequest = new HttpRequest();
				binaryRequest.Url = downloadUrl;
				binaryRequest.FailedHandler = Gamer.Cloud.HttpRequestFailedHandler;
				binaryRequest.Method = "GET";
				binaryRequest.TimeoutMillisec = Gamer.Cloud.HttpTimeoutMillis;
				binaryRequest.UserAgent = Gamer.Cloud.UserAgent;
				Common.RunRequest(binaryRequest, task, binaryResponse => {
					task.Resolve(binaryResponse.Body);
				}, forceClient: Managers.UnityHttpClient);
			});
		}

        /// <summary>Retrieves the binary data of an individual key from the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed. The binary data is attached as the value
        ///     of the result. Please ensure that the key was set with binary data before, or this call will
        ///     fail with a network error.</returns>
        /// <param name="key">The name of the key to be fetched.</param>
        public Promise<byte[]> GetBinary(string key)
        {
            UrlBuilder url = new UrlBuilder("/v3.0/gamer/vfs").Path(domain).Path(key).QueryParam("binary");
            HttpRequest req = Gamer.MakeHttpRequest(url);
            return Common.RunInTask<byte[]>(req, (response, task) => {
                // We must then download the received URL
                Bundle bundleRes = Bundle.FromJson(response.BodyString);
                Dictionary<string, Bundle> dict = bundleRes["result"].AsDictionary();
                string downloadUrl = "";
                foreach (var obj in dict)
                {
                    downloadUrl = obj.Value.AsString().Trim('"');
                    break;
                }
                HttpRequest binaryRequest = new HttpRequest();
                binaryRequest.Url = downloadUrl;
                binaryRequest.FailedHandler = Gamer.Cloud.HttpRequestFailedHandler;
                binaryRequest.Method = "GET";
                binaryRequest.TimeoutMillisec = Gamer.Cloud.HttpTimeoutMillis;
                binaryRequest.UserAgent = Gamer.Cloud.UserAgent;
                Common.RunRequest(binaryRequest, task, binaryResponse => {
                    task.Resolve(binaryResponse.Body);
                }, forceClient: Managers.UnityHttpClient);
            });
        }

        /// <summary>Sets the value of a key in the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed.</returns>
        /// <param name="key">The name of the key to set the value for.</param>
        /// <param name="value">The value to set. As usual with bundles, casting is implicitly done, so you may as well
        ///     call this method passing an integer or string as value for instance.</param>
        /// <remarks>This method is obsolete, use SetValue instead.</remarks>
        [Obsolete("Will be removed soon. Use SetValue instead.")]
        public Promise<Done> SetKey(string key, Bundle value)
        {
            UrlBuilder url = new UrlBuilder("/v1/gamer/vfs").Path(domain).Path(key);
            HttpRequest req = Gamer.MakeHttpRequest(url);
            req.BodyJson = Bundle.CreateObject("value", value);
            //req.BodyJson = value;
            req.Method = "PUT";
            return Common.RunInTask<Done>(req, (response, task) => {
                task.PostResult(new Done(response.BodyJson));
            });
        }

        /// <summary>Sets the value of a single key or all keys in the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed.</returns>
        /// <param name="key">The name of the key to set the value for.</param>
        /// <param name="value">The value to set. As usual with bundles, casting is implicitly done, so you may as well
        ///     call this method passing an integer or string as value for instance.</param>
        public Promise<Done> SetValue(string key, Bundle value)
        {
            UrlBuilder url = new UrlBuilder("/v3.0/gamer/vfs").Path(domain);
            if (key != null && key != "")
                url = url.Path(key);
            HttpRequest req = Gamer.MakeHttpRequest(url);
            //req.BodyJson = Bundle.CreateObject("value", value);
            req.BodyJson = value;
            req.Method = "PUT";
            return Common.RunInTask<Done>(req, (response, task) => {
                task.PostResult(new Done(response.BodyJson));
            });
        }


        /// <summary>Sets the value of a key in the key/value system as binary data.</summary>
        /// <returns>Promise resolved when the operation has completed.</returns>
        /// <param name="key">The name of the key to set the value for.</param>
        /// <param name="binaryData">The value to set as binary data.</param>
        /// <remarks>This method is obsolete, use SetBinary instead.</remarks>
        [Obsolete("Will be removed soon. Use SetBinary instead.")]
        public Promise<Done> SetKeyBinary(string key, byte[] binaryData)
        {
            UrlBuilder url = new UrlBuilder("/v1/gamer/vfs").Path(domain).Path(key).QueryParam("binary");
            HttpRequest req = Gamer.MakeHttpRequest(url);
            req.Method = "PUT";
            return Common.RunInTask<Done>(req, (response, task) => {
                // Now we have an URL to upload the data to
                HttpRequest binaryRequest = new HttpRequest();
                binaryRequest.Url = response.BodyJson["putURL"];
                binaryRequest.Body = binaryData;
                binaryRequest.FailedHandler = Gamer.Cloud.HttpRequestFailedHandler;
                binaryRequest.Method = "PUT";
                binaryRequest.TimeoutMillisec = Gamer.Cloud.HttpTimeoutMillis;
                binaryRequest.UserAgent = Gamer.Cloud.UserAgent;
                Common.RunRequest(binaryRequest, task, binaryResponse => {
                    task.Resolve(new Done(response.BodyJson));
                }, forceClient: Managers.UnityHttpClient);
            });
        }

        /// <summary>Sets the value of a key in the key/value system as binary data.</summary>
        /// <returns>Promise resolved when the operation has completed.</returns>
        /// <param name="key">The name of the key to set the value for.</param>
        /// <param name="binaryData">The value to set as binary data.</param>
        public Promise<Done> SetBinary(string key, byte[] binaryData)
        {
            UrlBuilder url = new UrlBuilder("/v3.0/gamer/vfs").Path(domain).Path(key).QueryParam("binary");
            if (s3ContentType != null)
                url = url.QueryParamEscaped("contentType", s3ContentType);

            HttpRequest req = Gamer.MakeHttpRequest(url);
            req.Method = "PUT";
            return Common.RunInTask<Done>(req, (response, task) => {
                // Now we have an URL to upload the data to
                HttpRequest binaryRequest = new HttpRequest();
                binaryRequest.Url = response.BodyJson["putURL"];
                binaryRequest.Body = binaryData;
                binaryRequest.FailedHandler = Gamer.Cloud.HttpRequestFailedHandler;
                binaryRequest.Method = "PUT";
                binaryRequest.TimeoutMillisec = Gamer.Cloud.HttpTimeoutMillis;
                binaryRequest.UserAgent = Gamer.Cloud.UserAgent;
                Common.RunRequest(binaryRequest, task, binaryResponse => {
                    task.Resolve(new Done(response.BodyJson));
                }, forceClient: Managers.UnityHttpClient);
            });
        }


        /// <summary>Removes a single key from the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed.</returns>
        /// <param name="key">The name of the key to remove.</param>
        /// <remarks>This method is obsolete, use DeleteValue instead.</remarks>
        [Obsolete("Will be removed soon. Use DeleteValue instead.")]
        public Promise<Done> RemoveKey(string key) {
			UrlBuilder url = new UrlBuilder("/v1/gamer/vfs").Path(domain).Path(key);
			HttpRequest req = Gamer.MakeHttpRequest(url);
			req.Method = "DELETE";
			return Common.RunInTask<Done>(req, (response, task) => {
				task.PostResult(new Done(response.BodyJson));
			});
		}

        /// <summary>Removes a single key or all keys from the key/value system.</summary>
        /// <returns>Promise resolved when the operation has completed.</returns>
        /// <param name="key">The name of the key to remove. Beware, if you don't pass any key at all,
        ///     then ALL the key/value will be removed. Should be used with care!</param>
        public Promise<Done> DeleteValue(string key=null)
        {
            UrlBuilder url = new UrlBuilder("/v3.0/gamer/vfs").Path(domain);
            if (key != null && key != "")
                url = url.Path(key);
            HttpRequest req = Gamer.MakeHttpRequest(url);
            req.Method = "DELETE";
            return Common.RunInTask<Done>(req, (response, task) => {
                task.PostResult(new Done(response.BodyJson));
            });
        }

        #region Private
        internal GamerVfs(Gamer gamer) {
			Gamer = gamer;
		}

		private string domain = Common.PrivateDomain;
		private Gamer Gamer;
		#endregion
	}
}
