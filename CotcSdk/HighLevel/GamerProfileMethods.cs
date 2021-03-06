
namespace CotcSdk
{
	/// @ingroup gamer_classes
	/// <summary>Exposes methods allowing to fetch and modify the profile of the signed in gamer.</summary>
	public sealed class GamerProfileMethods {

		/// <summary>
		/// Method used to retrieve some optional data of the logged in profile previously set by
		/// method SetProfile.
		/// </summary>
		/// <returns>Promise resolved when the operation has completed.</returns>
		public Promise<GamerProfile> Get() {
			HttpRequest req = Gamer.MakeHttpRequest("/v1/gamer/profile");
			return Common.RunInTask<GamerProfile>(req, (response, task) => {
				GamerProfile profile = new GamerProfile(response.BodyJson);
				task.PostResult(profile);
			});
		}

		/// <summary>
		/// Fetches an outline of the currently logged in user. Basically returns all available data about
		/// the user, including all domains he has been playing on. This can be used to avoid issuing
		/// multiple requests on startup (one for the profile, games, etc.).
		/// 
		/// Non exhaustive list of fields include: `network`, `networkid`, `networksecret`, `registerTime`,
		/// `registerBy`, `games` (array), `profile`, `devices` (array), `domains` (array), `serverTime`.
		/// </summary>
		/// <returns>Promise resolved when the operation has completed with the resulting outline.</returns>
		public Promise<GamerOutline> Outline() {
			HttpRequest req = Gamer.MakeHttpRequest("/v1/gamer/outline");
			return Common.RunInTask<GamerOutline>(req, (response, task) => {
				task.PostResult(new GamerOutline(response.BodyJson["outline"]));
			});
		}

		/// <summary>
		/// Method used to associate some optional data to the logged in profile in a JSON dictionary.
		/// You can fill fields with keys "email", "displayName", "lang", "firstName", "lastName",
		/// "addr1", "addr2", "addr3" and "avatar". Other fields will be ignored. These fields must be
		/// strings, and some are pre-populated when the account is created, using the available info
		/// from the social network used to create the account.
		/// </summary>
		/// <returns>Promise resolved when the operation has completed.</returns>
		/// <param name="data">Is a Bundle holding the data to save for this user. The object can hold the
		///     whole profile or just a subset of the keys.</param>
		public Promise<Done> Set(Bundle data) {
			HttpRequest req = Gamer.MakeHttpRequest("/v1/gamer/profile");
			req.BodyJson = data;
			return Common.RunInTask<Done>(req, (response, task) => {
				task.PostResult(new Done(response.BodyJson));
			});
		}

		#region Private
		internal GamerProfileMethods(Gamer parent) {
			Gamer = parent;
		}
		private Gamer Gamer;
		#endregion
	}
}
