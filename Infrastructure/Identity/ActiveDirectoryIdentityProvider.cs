using System.Security.Claims;

namespace ModelContextGateway.Infrastructure.Identity
{
    public class ActiveDirectoryIdentityProvider : IIdentityProvider
    {
        private readonly IConfiguration? _configuration;
        private readonly ILdapService? _ldapService;
        private readonly IAuthProviderRepository? _authRepo;
        private readonly IWindowsIdentityAccessor _windowsIdentityAccessor;

        public ActiveDirectoryIdentityProvider(
            IConfiguration? configuration = null,
            ILdapService? ldapService = null,
            IAuthProviderRepository? authRepo = null,
            IWindowsIdentityAccessor? windowsIdentityAccessor = null)
        {
            _configuration = configuration;
            _ldapService = ldapService;
            _authRepo = authRepo;
            _windowsIdentityAccessor = windowsIdentityAccessor ?? new WindowsIdentityAccessor();
        }

        public string ProviderName => "ActiveDirectory";

        public async Task<UserIdentityContext> ResolveIdentityAsync(HttpContext httpContext)
        {
            var config = _configuration ?? (httpContext.RequestServices?.GetService(typeof(IConfiguration)) as IConfiguration);
            var ldapService = _ldapService ?? (httpContext.RequestServices?.GetService(typeof(ILdapService)) as ILdapService);
            var authRepo = _authRepo ?? (httpContext.RequestServices?.GetService(typeof(IAuthProviderRepository)) as IAuthProviderRepository);

            if (!TrustedProxyHelper.IsTrustedProxy(httpContext, config))
            {
                TrustedProxyHelper.StripUntrustedHeaders(httpContext);
                return new UserIdentityContext("anonymous", ProviderName, new List<string>());
            }

            if (authRepo != null)
            {
                try
                {
                    var dbAuthProviders = await authRepo.GetAuthProvidersAsync();
                    var adDb = dbAuthProviders?.FirstOrDefault(p =>
                        string.Equals(p.ProviderName, "ActiveDirectory", System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.ProviderName, "LDAP", System.StringComparison.OrdinalIgnoreCase));

                    if (adDb != null && !adDb.IsEnabled)
                    {
                        return new UserIdentityContext("anonymous", ProviderName, new List<string>());
                    }
                }
                catch
                {
                    // Fallback to standard flow
                }
            }

            if (httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated &&
                !string.Equals(httpContext.User.Identity.AuthenticationType, "OidcHeader", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(httpContext.User.Identity.AuthenticationType, "AppKey", System.StringComparison.OrdinalIgnoreCase))
            {
                var username = httpContext.User.Identity.Name ?? "";
                string sid = "";
                var groups = new List<string>();
                var sids = new List<string>();

                if (_windowsIdentityAccessor.TryGetWindowsIdentityDetails(httpContext.User.Identity, out var winSid, out var winGroups))
                {
                    sid = winSid ?? "";
                    groups = winGroups;
                    sids.AddRange(winGroups);
                }
                else if (httpContext.User.Identity is ClaimsIdentity claimsIdentity)
                {
                    var primarySidClaim = claimsIdentity.FindFirst(ClaimTypes.PrimarySid);
                    if (primarySidClaim != null)
                    {
                        sid = primarySidClaim.Value;
                    }

                    var groupSids = claimsIdentity.FindAll(ClaimTypes.GroupSid)
                        .Select(c => c.Value)
                        .ToList();
                    sids.AddRange(groupSids);

                    var roles = claimsIdentity.FindAll(ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList();
                    groups = groupSids.Concat(roles).Distinct().ToList();
                }

                if (!string.IsNullOrEmpty(sid))
                {
                    sids.Add(sid);
                }

                if (ldapService != null && !string.IsNullOrEmpty(username))
                {
                    try
                    {
                        var ldapSids = await ldapService.ResolveUserSidsAsync(username);
                        if (ldapSids != null && ldapSids.Count > 0)
                        {
                            sids.AddRange(ldapSids);

                            // If primary SID is missing, take the first SID returned by LDAP
                            if (string.IsNullOrEmpty(sid))
                            {
                                sid = ldapSids.First();
                            }
                        }
                    }
                    catch (System.Exception exLdap)
                    {
                        var logger = httpContext.RequestServices?.GetService(typeof(ILogger<ActiveDirectoryIdentityProvider>))
                            as ILogger<ActiveDirectoryIdentityProvider>;
                        logger?.LogWarning(exLdap, "LDAP SID augmentation failed for {Username}; using token-group SIDs only.", username);
                    }
                }

                return new UserIdentityContext(username, ProviderName, groups, Sid: sid, Sids: sids.Distinct().ToList());
            }

            return new UserIdentityContext("anonymous", ProviderName, new List<string>());
        }
    }
}
