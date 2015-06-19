﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace CotcSdk
{
	[Serializable]
	public class CotcFacebookIntegration : MonoBehaviour {

		// Use this for initialization
		void Start() {
			FB.Init(() => {
				Debug.Log("FB initialized properly.");
				lock (DoWhenFbLoaded) {
					foreach (Action a in DoWhenFbLoaded) {
						a();
					}
					DoWhenFbLoaded.Clear();
					FbIsLoaded = true;
				}
			});
		}

		/**
		 * Logs in to CotC through facebook. This will bring an user interface allowing to sign in
		 * to facebook.
		 * @return task returning when the login has finished. The resulting Gamer object can then
		 *     be used for many purposes related to the signed in account.
		 * @param cloud needed to perform various tasks. Ensure that the SDK is initialized properly and fetch a
		 *     cloud object.
		 */
		public ResultTask<Gamer> LoginWithFacebook(Cloud cloud) {
			var task = new ResultTask<Gamer>();
			EnsureFacebookLoaded(() => {
				FB.Login("public_profile,email,user_friends", (FBResult result) => {
					if (result.Error != null) {
						task.PostResult(ErrorCode.SocialNetworkError, "Facebook/ " + result.Error);
					}
					else if (!FB.IsLoggedIn) {
						task.PostResult(ErrorCode.LoginCanceled);
					}
					else {
						string userId = FB.UserId, token = FB.AccessToken;
						Debug.Log("Logged in through facebook");
						cloud.Login(LoginNetwork.Facebook, userId, token)
							.ForwardTo(task);
					}
				});
			});
			return task;
		}

		/**
		 * Fetches the list of friends on facebook and sends them to CotC so that they automatically become friend with you.
		 * Note that this can only fetch the friends who are actually playing the game, so the list may be empty especially
		 * when in development.
		 * @param done callback invoked when the request has finished. The value is as returned by
		 *     #GamerCommunity.PostSocialNetworkFriends.
		 * @param gamer gamer object used to link the data to the account.
		 */
		public ResultTask<SocialNetworkFriendResponse> FetchFriends(Gamer gamer) {
			var task = new ResultTask<SocialNetworkFriendResponse>();
			EnsureFacebookLoaded(() => {
				DoFacebookRequestWithPagination((Result<List<SocialNetworkFriend>> result) => {
					if (!result.IsSuccessful) {
						task.PostResult(ErrorCode.SocialNetworkError, "Facebook request failed");
						return;
					}
					gamer.Community.PostSocialNetworkFriends(LoginNetwork.Facebook, result.Value)
						.ForwardTo(task);
				}, "/me/friends", Facebook.HttpMethod.GET);
			});
			return task;
		}

		#region Private
		// Starting point
		private void DoFacebookRequestWithPagination(ResultHandler<List<SocialNetworkFriend>> done, string query, Facebook.HttpMethod method) {
			FB.API(query, method, (FBResult result) => {
				DoFacebookRequestWithPagination(done, result, new List<SocialNetworkFriend>());
			});
		}

		// Recursive
		private void DoFacebookRequestWithPagination(ResultHandler<List<SocialNetworkFriend>> done, FBResult result, List<SocialNetworkFriend> addDataTo) {
			if (result.Error != null) {
				Debug.LogWarning("Error in facebook request: " + result.Error.ToString());
				Common.InvokeHandler(done, ErrorCode.SocialNetworkError, "Facebook/ Network #1");
				return;
			}

			// Gather the result from the last request
			try {
				Debug.Log("FB response: " + result.Text);
				Bundle fbResult = Bundle.FromJson(result.Text);
				List<Bundle> data = fbResult["data"].AsArray();
				foreach (Bundle element in data) {
					addDataTo.Add(new SocialNetworkFriend(element["id"], element["first_name"], element["last_name"], element["name"]));
				}
				string nextUrl = fbResult["paging"]["next"];
				// Finished
				if (data.Count == 0 || nextUrl == null) {
					Common.InvokeHandler(done, addDataTo, Bundle.Empty);
					return;
				}

				FB.API(nextUrl.Replace("https://graph.facebook.com", ""), Facebook.HttpMethod.GET, (FBResult res) => {
					DoFacebookRequestWithPagination(done, res, addDataTo);
				});
			}
			catch (Exception e) {
				Debug.LogError("Error decoding FB data: " + e.ToString());
				Common.InvokeHandler(done, ErrorCode.SocialNetworkError, "Decoding facebook data: " + e.Message);
				return;
			}
		}

		private void EnsureFacebookLoaded(Action a) {
			lock (DoWhenFbLoaded) {
				// Can do immediately
				if (FbIsLoaded) {
					a();
					return;
				}
				// Need to enqueue for later
				DoWhenFbLoaded.Add(a);
			}
		}

		private List<Action> DoWhenFbLoaded = new List<Action>();
		private bool FbIsLoaded = false;
		#endregion
	}
}