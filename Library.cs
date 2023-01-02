using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GenshinImpact_WishOnStreamGUI
{
    public class Character
    {
        public string CharacterName { get; set; }
        public string Element { get; set; }
        public int Star { get; set; }
    }

    public class CharacterListInStar : List<Character>
    {
        public Character this[string name]
        {
            get
            {
                Character character = FindCharacterByName(name);
                return character;
            }
        }
        public int StarValue { get; set; }
        public int PullRate { get; set; }

        public void Add(string name)
        {
            Character character = FindCharacterByName(name);
            if (character == null)
            {
                character = new();
                character.CharacterName = name;
                Add(character);
            }
        }
        public void Sort()
        {
            _characters = this.OrderBy(c => c.CharacterName).ToList();
            Clear();
            foreach (Character character in _characters)
                Add(character);
        }
        private Character FindCharacterByName(string characterName)
        {
            return this.FirstOrDefault(c => c.CharacterName == characterName);
        }

        private List<Character> _characters = new();
    }

    public class StarList : SortedDictionary<int, CharacterListInStar>
    {
        public CharacterListInStar this[int starValue]
        {
            get
            {
                CharacterListInStar charList = FindCharList(starValue);
                if (charList != null)
                    return charList;
                else
                    throw new Exception("Unable to find star collection");
            }
        }
        public Character this[string characterName]
        {
            get
            {
                Character character = new();

                foreach (KeyValuePair<int, CharacterListInStar> charList in this)
                {
                    CharacterListInStar characters = charList.Value;
                    Character thischar = characters[characterName];
                    if (thischar != null)
                    {
                        character = thischar;
                        break;
                    }
                }
                return character;
            }
        }
        public void AddStar(int starValue)
        {
            if (!ContainsKey(starValue))
            {
                CharacterListInStar charList = new();
                charList.StarValue = starValue;
                Add(starValue, charList);
            }
            _starList = this;
        }

        private CharacterListInStar FindCharList(int starValue)
        {
            return _starList[starValue];
        }
        private SortedDictionary<int, CharacterListInStar> _starList = new();
    }

    public static class Interwebs
    {
        public static readonly HttpClient httpClient = new();
    }

    public class AuthThings
    {
        MainWindow _mainwindow;
        public AuthThings(MainWindow mainwindow)
        {
            _mainwindow = mainwindow;
        }
        public string wisherPath = "";
        public UserInfo user;
        private const string CLIENT_ID = "rs83ihxx7l4k7jjeprsiz03ofvly8g";
        List<string> connectionErrors;

        // thanks to Philippe
        // https://stackoverflow.com/questions/29801195/adding-headers-when-using-httpclient-getasync
        public async void GetUserInfo(string access_token)
        {
            connectionErrors = new();
            ValidateToken(access_token);
            if (user.Name == "")
            {
                await AcquireToken(access_token);

                var rewards = await GetCustomRewards();
                _mainwindow.UpdateSettingsRewards(rewards);
                SaveCreds(user);
                _mainwindow.DisplayConnectionErrors(connectionErrors);
            }
            else
            {
                var rewards = await GetCustomRewards();
                _mainwindow.UpdateSettingsRewards(rewards);
                SaveCreds(user);
                _mainwindow.DisplayConnectionErrors(connectionErrors);
            }

        }

        private async Task<bool> ValidateToken(string token)
        {
            bool end = true;
            using HttpRequestMessage requestMessage = new(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
            requestMessage.Headers.Authorization = new("Bearer", token);
            try
            {
                HttpResponseMessage validateResponse = await Interwebs.httpClient.SendAsync(requestMessage);
                if (validateResponse.IsSuccessStatusCode)
                {
                    Task<string> validatePage = validateResponse.Content.ReadAsStringAsync();
                    TwitchToken receivedTokenInfo = JsonConvert.DeserializeObject<TwitchToken>(validatePage.Result);
                    user = new(receivedTokenInfo.Username, receivedTokenInfo.User_ID);
                    user.Token = token;
                    long timenow = DateTimeOffset.Now.ToUnixTimeSeconds();
                    long tokenExpiresInSeconds = int.Parse(receivedTokenInfo.TokenExpiresIn);
                    user.TokenExpiry = timenow + tokenExpiresInSeconds;
                }
            }
            catch
            {
                connectionErrors.Add("Unable to validate existing token.");
            }
            return end;
        }

        private async Task AcquireToken(string token)
        {
            using HttpRequestMessage requestMessage = new(HttpMethod.Get, "https://id.twitch.tv/oauth2/userinfo");
            requestMessage.Headers.Authorization = new("Bearer", token);
            try
            {
                HttpResponseMessage claimResponse = await Interwebs.httpClient.SendAsync(requestMessage);
                if (claimResponse.IsSuccessStatusCode)
                {
                    Task<string> claimPage = claimResponse.Content.ReadAsStringAsync();
                    TwitchClaims claimResult = JsonConvert.DeserializeObject<TwitchClaims>(claimPage.Result);

                    UserInfo userInfo = new(claimResult.Username, claimResult.User_ID);
                    userInfo.Token = token;
                    userInfo.TokenExpiry = int.Parse(claimResult.TokenExpiry);
                    if (user.ID == claimResult.User_ID)
                    {
                        userInfo.Redeem = user.Redeem;
                        userInfo.Duration = user.Duration;
                    }
                    user = userInfo;
                }
            }
            catch
            {
                connectionErrors.Add("Unable to acquire or refresh Twitch token.");
            }
        }

        public async void RevokeToken()
        {
            if (user.Token != "")
            {
                List<KeyValuePair<string, string>> data = new()
                {
                    new KeyValuePair<string, string>("client_id", CLIENT_ID),
                    new KeyValuePair<string, string>("token", user.Token),
                };
                FormUrlEncodedContent content = new(data);
                try
                {
                    await Interwebs.httpClient.PostAsync("https://id.twitch.tv/oauth2/revoke", content);
                }
                catch
                {
                    connectionErrors.Add("Unable to revoke token.");
                }
            }

            SaveCreds(user, true, true);
        }

        private async Task<List<string>> GetCustomRewards()
        {
            List<string> rewards = new();
            using HttpRequestMessage redeemRequest = new(HttpMethod.Get, "https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id=" + user.ID);
            Interwebs.httpClient.DefaultRequestHeaders.Add("Client-Id", CLIENT_ID);
            redeemRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.Token);

            try
            {
                HttpResponseMessage redeemResponse = await Interwebs.httpClient.SendAsync(redeemRequest);
                Task<string> redeemPage = redeemResponse.Content.ReadAsStringAsync();
                string longjson = redeemPage.Result;
                string[] separ = { "\"title\":\"" };
                string[] splits = longjson.Split(separ, StringSplitOptions.RemoveEmptyEntries);

                int ctr = 0;
                foreach (string split in splits)
                {
                    if (ctr != 0)
                    {
                        string search = "\",\"prompt\":";
                        int index = split.IndexOf(search);
                        rewards.Add(split.Substring(0, index));
                    }
                    ctr++;
                }
            }
            catch
            {
                connectionErrors.Add("Unable to fetch custom rewards.");
            }
            return rewards;
        }

        public string SaveCreds(UserInfo userInfo, bool saveToFile = false, bool revoke = false)
        {
            string pathSettings = Path.Combine(wisherPath, "js/local_creds.js");
            string errors = "";

            if (!File.Exists(pathSettings))
                errors += " - The \"local_creds.js\" file was not found in the \"" + wisherPath + "\"js folder.\n";
            if (!revoke)
            {
                if (userInfo.Name == "")
                    errors += " - Username was blank. Please connect using the Twitch button.\n";
                if (saveToFile && (userInfo.Redeem == ""))
                    errors += " - The Channel Point Redeem is not set. Please set this or make sure you have access to Twitch channel point rewards (Twitch Affiiate, etc.).";
            }
            else
            {
                userInfo = new();
                user = userInfo;
            }

            if (errors == "")
            {
                errors = "User settings saved successfully!";
                _mainwindow.UpdateSettingsPanel(userInfo);
                using StreamWriter writer = new(pathSettings);
                writer.WriteLine("var channelName = \'" + userInfo.Name + "\';");
                writer.WriteLine("var channelID = \'" + userInfo.ID + "\';");
                writer.WriteLine("var localToken = \'" + userInfo.Token + "\';");
                writer.WriteLine("var localTokenExpiry = " + userInfo.TokenExpiry + ";");
                writer.WriteLine("var redeemTitle = \'" + userInfo.Redeem + "\';");
                writer.WriteLine("var animation_duration = " + userInfo.Duration + ";");
            }
            else
            {
                errors = "User Settings errors:\n" + errors;
            }

            return errors;
        }
    }

    class Images
    {
        public static Bitmap Load(string imagename)
        {
            Assembly _assembly = Assembly.GetExecutingAssembly();
            Stream stream = _assembly.GetManifestResourceStream("GenshinImpact_WishOnStreamGUI.img." + imagename + ".png");
            Bitmap _bitmap = new(stream);
            return _bitmap;
        }
    }

    // thanks to Nick Charlton
    // https://thoughtbot.com/blog/using-httplistener-to-build-a-http-server-in-csharp
    class HttpServer
    {
        public int Port = 8275;
        const string localhostAddress = "http://localhost:8275/";

        private HttpListener _listener;

        private MainWindow _mainwindow;

        public void Start(MainWindow mainwindow)
        {
            _mainwindow = mainwindow;
            _listener = new HttpListener();
            _listener.Prefixes.Add(localhostAddress);
            _listener.Start();
            Receive();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        private void Receive()
        {
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
        }

        private void ListenerCallback(IAsyncResult result)
        {
            if (_listener.IsListening)
            {
                var context = _listener.EndGetContext(result);

                string htmlContent = "<html><body><script>" +
                    "var params = new URLSearchParams(window.location.hash.substring(1));" +
                    "var token = params.get('access_token');" +
                    "var newurl = \"" + localhostAddress + "?access_token=\" + token;" +
                    "if (token != null) window.location.href = newurl;" +
                    "</script></body></html>";
                string access_token = System.Web.HttpUtility.ParseQueryString(context.Request.Url.Query).Get("access_token");

                // if access_token is set, get user info associated with the token
                if (access_token != null)
                {
                    _mainwindow.authVar.GetUserInfo(access_token);
                    htmlContent = "<style>" +
                        "#link_to_token {" +
                        "font-family: sans-serif; font-weight: bold; font-size: 1.2rem; text-align: center; display: block; padding: 1em;" +
                        "background: #59f; color: #fff; text-decoration: none; width: 50%; text-shadow: 2px 2px 2px rgb(0 0 0 / 30%);" +
                        "min-width: 400px; margin: 2em auto; border-radius: 15px;" +
                        "}" +
                        ".error { background: #f95; }" +
                        "</style>";
                    htmlContent += "<p id=\"link_to_token\">You may now close this window and/or refresh the Genshin Wisher browser source in your streaming software.</a>";
                }

                byte[] _responseArray = System.Text.Encoding.UTF8.GetBytes(htmlContent); // get the bytes to response
                context.Response.OutputStream.Write(_responseArray, 0, _responseArray.Length); // write bytes to the output stream
                context.Response.KeepAlive = false; // set the KeepAlive bool to false
                context.Response.Close(); // close the connection

                Receive();
            }
        }
    }

    public class UserInfo
    {
        string _name;
        string _id;
        string _token;
        long _expiry;
        string _redeem;
        int _duration;
        public UserInfo()
        {
            _name = "";
            _id = "";
            _token = "";
            _expiry = 0;
            _redeem = "";
            _duration = 8000;
        }
        public UserInfo(string name, string id)
        {
            _name = name;
            _id = id;
            _token = "";
            _expiry = 0;
            _redeem = "";
            _duration = 8000;
        }
        public string Name => _name;
        public string ID => _id;
        public string Redeem { get => _redeem; set => _redeem = value; }
        public int Duration { get => _duration; set => _duration = value; }
        public string Token { get => _token; set => _token = value; }
        public long TokenExpiry { get => _expiry; set => _expiry = value; }
    }

    #region JSON classes
    class TwitchClaims
    {
        [JsonProperty("exp")]
        public string TokenExpiry { get; set; }
        [JsonProperty("iat")]
        public string TokenIssued { get; set; }
        [JsonProperty("sub")]
        public string User_ID { get; set; }
        [JsonProperty("preferred_username")]
        public string Username { get; set; }
    }
    class TwitchToken
    {
        [JsonProperty("client_id")]
        public string Client_ID { get; set; }
        [JsonProperty("login")]
        public string Username { get; set; }
        [JsonProperty("scopes")]
        public List<string> Scopes { get; set; }
        [JsonProperty("user_id")]
        public string User_ID { get; set; }
        [JsonProperty("expires_in")]
        public string TokenExpiresIn { get; set; }
    }
    class VersionClass
    {
        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
        [JsonProperty("downloadurl")]
        public string DownloadURL { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
    }
    #endregion

}
