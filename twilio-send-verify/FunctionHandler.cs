using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Function
{
    /// <summary>
    /// Send a verify command from Twilio to a target To via a Channel (sms, call, email)
    /// https://www.twilio.com/docs/verify/api
    /// 
    /// FYI, for the email channel, a Mailer must be associated with the service in Twilio before using the email channel
    /// </summary>
    public class FunctionHandler
    {
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            try
            {
                if (request.Body == null)
                {
                    var msg = "Request body was null";
                    await Console.Error.WriteLineAsync(msg);
                    return ((int)HttpStatusCode.BadRequest, $"{string.Empty}");
                }

                // parse the request body
                var reader = new StreamReader(request.Body);
                var input = await reader.ReadToEndAsync();
                var cmdObj = JObject.Parse(input);

                // assemble our command to verify
                var to = (string)cmdObj["To"];
                var channel = (string)cmdObj["Channel"];
                var cmd = new SendVerifyCodeCommand(to, channel);

                // call the verify API
                var response = await GetHttpResponse(cmd);
                if (response.IsSuccessStatusCode)
                {
                    return ((int)HttpStatusCode.OK, $"{string.Empty}");
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var msg = string.Format("Unexpected server error: {0} {1}",
                        (int)response.StatusCode, responseBody);
                    await Console.Error.WriteLineAsync(msg);
                    return ((int)HttpStatusCode.InternalServerError, $"{string.Empty}");
                }
            }
            catch (ArgumentNullException e)
            {
                var msg = string.Format("Missing input: {0}", e.ToString());
                await Console.Error.WriteLineAsync(msg);
                return ((int)HttpStatusCode.BadRequest, $"{string.Empty}");
            }
            catch (ArgumentException e)
            {
                var msg = string.Format("Bad input: {0}", e.ToString());
                await Console.Error.WriteLineAsync(msg);
                return ((int)HttpStatusCode.BadRequest, $"{string.Empty}");
            }
            catch (Exception e)
            {
                var msg = string.Format("Unexpected server error: {0}", e.ToString());
                await Console.Error.WriteLineAsync(msg);
                return ((int)HttpStatusCode.InternalServerError, $"{string.Empty}");
            }
        }

        /// <summary>        
        /// This sends a POST to the Twilio Verify API. It assumes:
        /// 1. You have the secrets available to your OpenFaas function
        /// 2. You have the twilio_verify_endpoint URL defined as an environment variable        
        /// </summary>
        /// <param name="command">A target to send the Verify code to</param>
        /// <returns>The HTTP response from Twilio</returns>
        async private Task<HttpResponseMessage> GetHttpResponse(SendVerifyCodeCommand command)
        {
            string accountSid;
            string authToken;
            using (var reader = File.OpenText("/var/openfaas/secrets/twilio-account-sid"))
            {
                var text = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentNullException("accountSid is null");
                }
                accountSid = text;
            }

            using (var reader = File.OpenText("/var/openfaas/secrets/twilio-auth-token"))
            {
                var text = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentNullException("authToken is null");
                }
                authToken = text;
            }

            // setup authorization header
            var uri = Environment.GetEnvironmentVariable("twilio_verify_endpoint");
            var basicToken = $"{accountSid}:{authToken}";
            var authHeader = $"{basicToken}";
            var encodedAuthHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(authHeader));

            // setup client and add the authorization header
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("authorization", $"Basic {encodedAuthHeader}");

            // build our request
            var sb = new StringBuilder();
            var encodedNumber = HttpUtility.UrlEncode(string.Format("+{0}", command.To));
            sb.Append(string.Format("To={0}", encodedNumber));
            sb.Append("&");
            var encodedChannel = HttpUtility.UrlEncode(command.Channel);
            sb.Append(string.Format("Channel={0}", encodedChannel));
            var body = sb.ToString();
            var requestBody = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            // send the POST to Twilio
            return await client.PostAsync(uri, requestBody);
        }
    }

    class SendVerifyCodeCommand
    {
        public SendVerifyCodeCommand(String to, String channel)
        {
            if (String.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentNullException("To is missing");
            }
            if (String.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentNullException("Channel is missing");
            }
            var validChannels = new List<string>()
            {
                "sms",
                "call",
                "email"
            };

            if (!validChannels.Contains(channel.Trim().ToLower()))
            {
                throw new ArgumentException("Channel must be sms, call,or email");
            }

            if (channel.Equals("email"))
            {
                try
                {
                    var email = new MailAddress(to);
                }
                catch (Exception)
                {
                    throw new ArgumentException("Email is invalid");
                }
            }
            else // it's call or sms
            {
                if (!to.Any(char.IsDigit))
                {
                    throw new ArgumentException("Phone must be digits");
                }
            }

            this.To = to.Trim();
            this.Channel = channel.Trim();
        }

        public string To { get; set; }
        public string Channel { get; set; }
    }
}