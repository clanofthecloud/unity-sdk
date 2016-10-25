using UnityEngine;
using System;
using System.Collections;
using System.Text;

namespace CotcSdk {
	public class CotcCoroutinesManager : MonoBehaviour {
		public static CotcCoroutinesManager instance;

		public void Awake() {
			instance = this;
		}

		#region Event Loop Coroutine
		private DomainEventLoop domainEventLoop;
		private int eventLoopDelay;
		private string eventLoopMsgToAck;
		private bool eventLoopLastResultPositive = true;

		internal void StartEventLoopCoroutine(DomainEventLoop del = null) {
			if (del != null) {
				domainEventLoop = del;
				eventLoopDelay = del.LoopIterationDuration;
			}
			StartCoroutine("EventLoopCoroutine");
		}

		internal void StopEventLoopCoroutine() {
			StopCoroutine("EventLoopCoroutine");
		}

		private IEnumerator EventLoopCoroutine() {
			// In case of stop, prevent to continue the coroutine if DomainEventLoop hasn't (shouldn't happen)
			while (!domainEventLoop.Stopped) {
				if (!eventLoopLastResultPositive) {
					// Last time failed, wait a bit to avoid bombing the Internet.
					yield return new WaitForSeconds(DomainEventLoop.PopEventDelayCoroutineHold);
				}

				UrlBuilder url = new UrlBuilder("/v1/gamer/event");
				url.Path(domainEventLoop.Domain).QueryParam("timeout", eventLoopDelay);
				if (eventLoopMsgToAck != null) {
					url.QueryParam("ack", eventLoopMsgToAck);
				}
				
				domainEventLoop.CurrentRequest = domainEventLoop.Gamer.MakeHttpRequest(url);
				domainEventLoop.CurrentRequest.RetryPolicy = HttpRequest.Policy.NonpermanentErrors;
				domainEventLoop.CurrentRequest.TimeoutMillisec = eventLoopDelay + 30000;
				domainEventLoop.CurrentRequest.DoNotEnqueue = true;
				
				Managers.HttpClient.Run(domainEventLoop.CurrentRequest, (HttpResponse res) => {
					domainEventLoop.CurrentRequest = null;
					try {
						eventLoopLastResultPositive = true;
						if (res.StatusCode == 200) {
							eventLoopMsgToAck = res.BodyJson["id"];
							ProcessEvent(res);
						} else if (res.StatusCode != 204) {
							eventLoopLastResultPositive = false;
							// Non retriable error -> kill ourselves
							if (res.StatusCode >= 400 && res.StatusCode < 500) {
								domainEventLoop.Stopped = true;
							}
						}
					} catch (Exception e) {
						Common.LogError("Exception happened in pop event loop: " + e.ToString());
					}
					// Start a new loop after response or timeout are processed if not paused or stoped
					StopEventLoopCoroutine();
					StartEventLoopCoroutine();
				});
				
				// After timeout is reached but not processed, continue to a new loop if the HttpResponse's delegate hasn't (shouldn't happen)
				yield return new WaitForSeconds((domainEventLoop.CurrentRequest.TimeoutMillisec / 1000) + 10);
			}
			
			yield return null;
		}

		private void ProcessEvent(HttpResponse res) {
			try {
				EventLoopArgs args = new EventLoopArgs(res.BodyJson);
				if (domainEventLoop.receivedEvent != null) domainEventLoop.receivedEvent(domainEventLoop, args);
				Cotc.NotifyReceivedMessage(domainEventLoop, args);
			}
			catch (Exception e) {
				Common.LogError("Exception in the event chain: " + e.ToString());
			}
		}
		#endregion

		#region Timeout Coroutine
		private class TimeoutParameters {
			public Action<object> TimeoutCallback;
			public object state;
			public float timeoutSec;

			public TimeoutParameters(Action<object> _TimeoutCallback, object _state, float _timeoutSec)
			{
				TimeoutCallback = _TimeoutCallback;
				state = _state;
				timeoutSec = _timeoutSec;
			}
		}

		internal void StartTimeoutCoroutine(Action<object> TimeoutCallback, object state, int timeoutMillisec) {
			TimeoutParameters timeoutParameters = new TimeoutParameters(TimeoutCallback, state, ((float)timeoutMillisec) / 1000f);
			StartCoroutine("TimeoutCoroutine", timeoutParameters);
		}

		private IEnumerator TimeoutCoroutine(TimeoutParameters timeoutParameters) {
			yield return new WaitForSeconds(timeoutParameters.timeoutSec);
			timeoutParameters.TimeoutCallback(timeoutParameters.state);
			yield return null;
		}
		#endregion

		#region Request Failed Retry Coroutine
		private class RequestFailedRetryParameters {
			public Action RetryCallback;
			public float timeoutSec;

			public RequestFailedRetryParameters(Action _RetryCallback, float _timeoutSec)
			{
				RetryCallback = _RetryCallback;
				timeoutSec = _timeoutSec;
			}
		}

		internal void StartRequestFailedRetryCoroutine(Action RetryCallback, int timeoutMillisec) {
			RequestFailedRetryParameters requestFailedRetryParameters = new RequestFailedRetryParameters(RetryCallback, ((float)timeoutMillisec) / 1000f);
			StartCoroutine("RequestFailedRetryCoroutine", requestFailedRetryParameters);
		}

		private IEnumerator RequestFailedRetryCoroutine(RequestFailedRetryParameters requestFailedRetryParameters) {
			yield return new WaitForSeconds(requestFailedRetryParameters.timeoutSec);
			requestFailedRetryParameters.RetryCallback();
			yield return null;
		}
		#endregion
	}
}