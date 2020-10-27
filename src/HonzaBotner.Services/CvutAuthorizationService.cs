using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HonzaBotner.Database;
using HonzaBotner.Services.Contract;
using HonzaBotner.Services.Contract.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Text.Json;

namespace HonzaBotner.Services
{
    public class CvutAuthorizationService : IAuthorizationService
    {
        private readonly HonzaBotnerDbContext _dbContext;
        private readonly CvutConfig _cvutConfig;
        private readonly IUsermapInfoService _usermapInfoService;
        private readonly IDiscordRoleManager _roleManager;
        private readonly HttpClient _client;
        private readonly IHashService _hashService;

        public CvutAuthorizationService(HonzaBotnerDbContext dbContext, IOptions<CvutConfig> cvutConfig,
            IUsermapInfoService usermapInfoService, IDiscordRoleManager roleManager, HttpClient client,
            IHashService hashService)
        {
            _dbContext = dbContext;
            _cvutConfig = cvutConfig.Value;
            _usermapInfoService = usermapInfoService;
            _roleManager = roleManager;
            _client = client;
            _hashService = hashService;
        }

        public async Task<bool> AuthorizeAsync(string accessToken, string username, ulong userId)
        {
            if (await IsUserVerified(userId))
            {
                return false;
            }

            UsermapPerson? person = await _usermapInfoService.GetUserInfoAsync(accessToken, username);
            if (person == null)
            {
                return false;
            }

            string authId = _hashService.Hash(person.Username);
            if (await _dbContext.Verifications.AnyAsync(v => v.AuthId == authId))
            {
                return false;
            }

            IReadOnlySet<DiscordRole> discordRoles = _roleManager.MapUsermapRoles(person.Roles);
            bool rolesGranted = await _roleManager.GrantRolesAsync(userId, discordRoles);

            if (rolesGranted)
            {
                Verification verification = new Verification() {AuthId = authId, UserId = userId};

                await _dbContext.Verifications.AddAsync(verification);
                await _dbContext.SaveChangesAsync();
            }

            return rolesGranted;
        }

        public Task<string> GetAuthLinkAsync(string redirectUri)
        {
            const string authLink =
                "https://auth.fit.cvut.cz/oauth/authorize?response_type=code&client_id={0}&redirect_uri={1}";

            if (string.IsNullOrEmpty(_cvutConfig.ClientId))
            {
                throw new ArgumentNullException(null, "Invalid config");
            }

            return Task.FromResult(string.Format(authLink, _cvutConfig.ClientId, redirectUri));
        }

        public async Task<bool> IsUserVerified(ulong userId)
        {
            return await _dbContext.Verifications
                .AnyAsync(v => v.UserId == userId);
        }

        private string GetQueryString(NameValueCollection queryCollection)
            => string.Join('&', queryCollection.AllKeys.Select(k => $"{k}={HttpUtility.UrlEncode(queryCollection[k])}"));

        public async Task<string> GetAccessTokenAsync(string code, string redirectUri)
        {
            const string tokenUri = "https://auth.fit.cvut.cz/oauth/token";

            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(_cvutConfig.ClientId + ":" + _cvutConfig.ClientSecret));
            NameValueCollection queryCollection = new NameValueCollection
            {
                {"grant_type", "authorization_code"}, {"code", code}, {"redirect_uri", redirectUri}
            };

            var uriBuilder =new UriBuilder(tokenUri)
            {
                Query = GetQueryString(queryCollection)
            };

            HttpRequestMessage requestMessage = new HttpRequestMessage()
            {
                RequestUri = uriBuilder.Uri,
                Headers = {Authorization = new AuthenticationHeaderValue("Basic", credentials)},
                Method = HttpMethod.Post
            };

            HttpResponseMessage tokenResponse = await _client.SendAsync(requestMessage);
            tokenResponse.EnsureSuccessStatusCode();

            JsonDocument response = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync());

            return response.RootElement.GetProperty("access_token").GetString()
                   ?? throw new InvalidOperationException("Couldn't authorize user");
        }

        public async Task<string> GetUserNameAsync(string accessToken)
        {
            const string checkTokenUri = "https://auth.fit.cvut.cz/oauth/check_token";

            var uriBuilder =new UriBuilder(checkTokenUri)
            {
                Query = $"token={accessToken}"
            };
            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);

            HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            string responseText = await response.Content.ReadAsStringAsync();
            var user = JsonDocument.Parse(responseText);

            return user.RootElement.GetProperty("user_name").GetString()
                   ?? throw new InvalidOperationException("Couldn't load information about user");
        }
    }
}