import React, { useState } from 'react';
import { AuthProviderConfig } from '../../shared/types';
import { testLdapConnectionApi } from '../../api/settingsApi';
import { showToast } from '../../stores/useToastStore';

export interface IdentityAuthTabProps {
  providers: AuthProviderConfig[];
  saveAuthProvider: (provider: AuthProviderConfig) => Promise<void>;
}

export const IdentityAuthTab: React.FC<IdentityAuthTabProps> = ({ providers, saveAuthProvider }) => {
  const ad = providers.find((p) => p.providerName === 'ActiveDirectory');
  const oidc = providers.find(
    (p) => p.providerName === 'HeaderAuth' || p.providerName === 'Oidc' || p.providerName === 'PocketID_TinyAuth' || p.providerName === 'PocketID'
  );
  const extJwt = providers.find((p) => p.providerName === 'ExternalJwt' || p.providerName === 'InHouseIdP');

  const parsedExtJwt = extJwt?.configJson
    ? (() => {
        try {
          return JSON.parse(extJwt.configJson);
        } catch {
          return {};
        }
      })()
    : {};

  const [authExtJwtEnabled, setAuthExtJwtEnabled] = useState(extJwt ? extJwt.isEnabled : false);
  const [authExtJwtAuthority, setAuthExtJwtAuthority] = useState(parsedExtJwt.authority || '');
  const [authExtJwtAudience, setAuthExtJwtAudience] = useState(parsedExtJwt.audience || '');
  const [authExtJwtIssuer, setAuthExtJwtIssuer] = useState(parsedExtJwt.issuer || '');

  const parsedAd = ad?.configJson
    ? (() => {
        try {
          return JSON.parse(ad.configJson);
        } catch {
          return {};
        }
      })()
    : {};

  const [adDecryptionFailed, setAdDecryptionFailed] = useState(ad?.isDecryptionFailed || false);
  const [authAdEnabled, setAuthAdEnabled] = useState(ad ? ad.isEnabled : false);
  const [authAdServer, setAuthAdServer] = useState(parsedAd.server || '');
  const [authAdPort, setAuthAdPort] = useState(parsedAd.port !== undefined ? String(parsedAd.port) : '636');
  const [authAdUseSsl, setAuthAdUseSsl] = useState(parsedAd.useSsl !== undefined ? parsedAd.useSsl : true);
  const [authAdDomain, setAuthAdDomain] = useState(parsedAd.domain || '');
  const [authAdBaseDn, setAuthAdBaseDn] = useState(parsedAd.baseDn || '');
  const [authAdBindDn, setAuthAdBindDn] = useState(parsedAd.bindDn || '');
  const [authAdBindPassword, setAuthAdBindPassword] = useState(parsedAd.bindPassword || '');

  const [adTestStatus, setAdTestStatus] = useState<{
    type: 'idle' | 'testing' | 'success' | 'error';
    message: string;
  }>({ type: 'idle', message: '' });

  const [authOidcEnabled, setAuthOidcEnabled] = useState(oidc ? oidc.isEnabled : true);
  const [authUserHeader, setAuthUserHeader] = useState(oidc?.userHeader || 'Remote-User');
  const [authGroupsHeader, setAuthGroupsHeader] = useState(oidc?.groupsHeader || 'Remote-Groups');

  const handleTestLdap = async () => {
    setAdTestStatus({ type: 'testing', message: 'Testing LDAP connection...' });
    try {
      const res = await testLdapConnectionApi({
        server: authAdServer,
        port: parseInt(authAdPort, 10) || 636,
        useSsl: authAdUseSsl,
        domain: authAdDomain,
        baseDn: authAdBaseDn,
        bindDn: authAdBindDn,
        bindPassword: authAdBindPassword,
      });

      if (res.success) {
        setAdTestStatus({ type: 'success', message: res.message || 'LDAP connection successful!' });
      } else {
        setAdTestStatus({ type: 'error', message: res.error || 'LDAP connection failed.' });
      }
    } catch (err: any) {
      setAdTestStatus({ type: 'error', message: err?.message || 'Error executing LDAP test.' });
    }
  };

  const handleSaveAuthProviders = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const adConfig = {
        server: authAdServer,
        port: parseInt(authAdPort, 10) || 636,
        useSsl: authAdUseSsl,
        domain: authAdDomain,
        baseDn: authAdBaseDn,
        bindDn: authAdBindDn,
        bindPassword: authAdBindPassword,
      };

      await saveAuthProvider({
        providerName: 'ActiveDirectory',
        displayName: ad?.displayName || 'Active Directory LDAP',
        userHeader: 'Remote-User',
        groupsHeader: 'Remote-Groups',
        configJson: adDecryptionFailed ? (ad?.configJson || '') : JSON.stringify(adConfig),
        isEnabled: authAdEnabled,
        isDecryptionFailed: adDecryptionFailed,
      });

      await saveAuthProvider({
        providerName: oidc?.providerName || 'HeaderAuth',
        displayName: oidc?.displayName || 'OIDC / Reverse Proxy Headers',
        userHeader: authUserHeader,
        groupsHeader: authGroupsHeader,
        isEnabled: authOidcEnabled,
      });

      if (extJwt || authExtJwtEnabled || authExtJwtAuthority) {
        await saveAuthProvider({
          providerName: extJwt?.providerName || 'ExternalJwt',
          displayName: extJwt?.displayName || 'In-House IdP / External JWT Bearer',
          userHeader: '',
          groupsHeader: '',
          configJson: JSON.stringify({
            authority: authExtJwtAuthority,
            audience: authExtJwtAudience,
            issuer: authExtJwtIssuer,
          }),
          isEnabled: authExtJwtEnabled,
        });
      }

      showToast('Auth Provider configurations saved successfully!', 'success');
    } catch {
      showToast('Failed to save Auth Providers', 'error');
    }
  };

  return (
    <div id="subview-identity" className="settings-subview active">
      <div className="glass-card settings-card" style={{ maxWidth: '850px', margin: '0 auto' }}>
        <h2>
          <i className="fa-solid fa-id-card"></i> Identity &amp; Auth Providers
        </h2>
        <p className="description" style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '20px' }}>
          Enable authentication providers for user identity context, Windows SID resolution, and group policy authorization.
        </p>
        <form id="auth-providers-form" onSubmit={handleSaveAuthProviders}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 0.8fr', gap: '20px' }}>
            {/* Active Directory / LDAP */}
            <div style={{ padding: '15px', background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)', borderRadius: '8px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                <h4 style={{ margin: 0 }}>
                  <i className="fa-solid fa-brands fa-windows"></i> Active Directory / LDAP
                </h4>
                <label className="switch">
                  <input
                    type="checkbox"
                    id="auth-ad-enabled"
                    checked={authAdEnabled}
                    onChange={(e) => setAuthAdEnabled(e.target.checked)}
                  />
                  <span className="slider"></span>
                </label>
              </div>
              <p style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Authenticate users via Active Directory Kerberos/NTLM or LDAP service binds.</p>

              {adDecryptionFailed && (
                <div className="alert alert-warning mb-4">
                  <strong>Decryption Failed:</strong> The configuration for Active Directory could not be decrypted. 
                  <button type="button" onClick={() => setAdDecryptionFailed(false)} className="btn btn-sm btn-outline-danger ms-3">Reset Config</button>
                </div>
              )}

              {authAdEnabled && (
                <div id="ad-config-fields" style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '10px' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '8px' }}>
                    <div className="form-group" style={{ margin: 0 }}>
                      <label htmlFor="ad-server" style={{ fontSize: '11px' }}>LDAP Server Host</label>
                      <input
                        type="text"
                        id="ad-server"
                        disabled={adDecryptionFailed}
                        placeholder="ad.corp.local"
                        value={authAdServer}
                        onChange={(e) => setAuthAdServer(e.target.value)}
                        style={{ fontSize: '12px' }}
                      />
                    </div>
                    <div className="form-group" style={{ margin: 0 }}>
                      <label htmlFor="ad-port" style={{ fontSize: '11px' }}>Port</label>
                      <input
                        type="text"
                        id="ad-port"
                        disabled={adDecryptionFailed}
                        placeholder="636"
                        value={authAdPort}
                        onChange={(e) => setAuthAdPort(e.target.value)}
                        style={{ fontSize: '12px' }}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '2px' }}>
                    <label className="switch">
                      <input
                        type="checkbox"
                        id="ad-use-ssl"
                        disabled={adDecryptionFailed}
                        checked={authAdUseSsl}
                        onChange={(e) => setAuthAdUseSsl(e.target.checked)}
                      />
                      <span className="slider"></span>
                    </label>
                    <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Use LDAPS (SSL Encrypted)</span>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginTop: '4px' }}>
                    <div className="form-group" style={{ margin: 0 }}>
                      <label htmlFor="ad-domain" style={{ fontSize: '11px' }}>Domain Name</label>
                      <input
                        type="text"
                        id="ad-domain"
                        disabled={adDecryptionFailed}
                        placeholder="e.g. corp.local"
                        value={authAdDomain}
                        onChange={(e) => setAuthAdDomain(e.target.value)}
                        style={{ fontSize: '12px' }}
                      />
                    </div>
                    <div className="form-group" style={{ margin: 0 }}>
                      <label htmlFor="ad-base-dn" style={{ fontSize: '11px' }}>Base DN</label>
                      <input
                        type="text"
                        id="ad-base-dn"
                        disabled={adDecryptionFailed}
                        placeholder="DC=corp,DC=local"
                        value={authAdBaseDn}
                        onChange={(e) => setAuthAdBaseDn(e.target.value)}
                        style={{ fontSize: '12px' }}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginTop: '4px' }}>
                    <div className="form-group" style={{ margin: 0 }}>
                      <label htmlFor="ad-bind-dn" style={{ fontSize: '11px' }}>Bind DN / User</label>
                      <input
                        type="text"
                        id="ad-bind-dn"
                        disabled={adDecryptionFailed}
                        placeholder="CN=admin,DC=corp,DC=local"
                        value={authAdBindDn}
                        onChange={(e) => setAuthAdBindDn(e.target.value)}
                        style={{ fontSize: '12px' }}
                      />
                    </div>
                    <div className="form-group" style={{ margin: 0 }}>
                      <label htmlFor="ad-bind-password" style={{ fontSize: '11px' }}>Bind Password</label>
                      <input
                        type="password"
                        id="ad-bind-password"
                        disabled={adDecryptionFailed}
                        placeholder="Password"
                        value={authAdBindPassword}
                        onChange={(e) => setAuthAdBindPassword(e.target.value)}
                        style={{ fontSize: '12px' }}
                      />
                    </div>
                  </div>

                  <div style={{ marginTop: '10px', display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <button
                      type="button"
                      id="btn-test-ldap"
                      className="btn btn-secondary btn-sm"
                      onClick={handleTestLdap}
                      disabled={adTestStatus.type === 'testing'}
                    >
                      <i className="fa-solid fa-plug"></i> {adTestStatus.type === 'testing' ? 'Testing...' : 'Test Connection'}
                    </button>
                    {adTestStatus.type !== 'idle' && (
                      <span
                        id="ad-test-feedback"
                        style={{
                          fontSize: '11px',
                          color: adTestStatus.type === 'success' ? '#10b981' : adTestStatus.type === 'error' ? '#ef4444' : 'var(--text-muted)'
                        }}
                      >
                        {adTestStatus.message}
                      </span>
                    )}
                  </div>
                </div>
              )}
            </div>

            {/* OIDC / Reverse Proxy Headers */}
            <div style={{ padding: '15px', background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)', borderRadius: '8px', height: 'fit-content' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                <h4 style={{ margin: 0 }}>
                  <i className="fa-solid fa-key"></i> OIDC / Reverse Proxy Headers
                </h4>
                <label className="switch">
                  <input
                    type="checkbox"
                    id="auth-oidc-enabled"
                    checked={authOidcEnabled}
                    onChange={(e) => setAuthOidcEnabled(e.target.checked)}
                  />
                  <span className="slider"></span>
                </label>
              </div>
              <p style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Parse user and group headers from SSO reverse proxies (Authentik, Authelia, PocketID, Keycloak, Caddy, Traefik, etc.).</p>
              {authOidcEnabled && (
                <div className="alert alert-warning mb-3 mt-2" style={{ fontSize: '12px' }}>
                  Security Warning: Ensure your router is running behind a trusted reverse proxy that securely sets these headers and strips them from incoming requests. Otherwise, clients could spoof their identity.
                </div>
              )}
              <div className="form-group" style={{ marginTop: '10px' }}>
                <label htmlFor="auth-user-header" style={{ fontSize: '11px' }}>
                  User Header Name
                </label>
                <input
                  type="text"
                  id="auth-user-header"
                  value={authUserHeader}
                  onChange={(e) => setAuthUserHeader(e.target.value)}
                  style={{ fontSize: '12px' }}
                />
              </div>
              <div className="form-group" style={{ marginTop: '5px' }}>
                <label htmlFor="auth-groups-header" style={{ fontSize: '11px' }}>
                  Groups Header Name
                </label>
                <input
                  type="text"
                  id="auth-groups-header"
                  value={authGroupsHeader}
                  onChange={(e) => setAuthGroupsHeader(e.target.value)}
                  style={{ fontSize: '12px' }}
                />
              </div>
            </div>

            {/* In-House IdP / External JWT Bearer */}
            <div style={{ padding: '15px', background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)', borderRadius: '8px', gridColumn: 'span 2' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                <h4 style={{ margin: 0 }}>
                  <i className="fa-solid fa-shield-halved"></i> In-House IdP / External JWT Bearer
                </h4>
                <label className="switch">
                  <input
                    type="checkbox"
                    id="auth-external-jwt-enabled"
                    checked={authExtJwtEnabled}
                    onChange={(e) => setAuthExtJwtEnabled(e.target.checked)}
                  />
                  <span className="slider"></span>
                </label>
              </div>
              <p style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                Validate incoming client Bearer JWTs against an enterprise Identity Provider using standard OIDC discovery (<code>.well-known/openid-configuration</code>) and JWKS public keys.
              </p>
              {authExtJwtEnabled && (
                <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 1fr', gap: '10px', marginTop: '10px' }}>
                  <div className="form-group" style={{ margin: 0 }}>
                    <label htmlFor="auth-jwt-authority" style={{ fontSize: '11px' }}>Authority / Discovery URL</label>
                    <input
                      type="text"
                      id="auth-jwt-authority"
                      placeholder="https://idp.corp.local"
                      value={authExtJwtAuthority}
                      onChange={(e) => setAuthExtJwtAuthority(e.target.value)}
                      style={{ fontSize: '12px' }}
                    />
                  </div>
                  <div className="form-group" style={{ margin: 0 }}>
                    <label htmlFor="auth-jwt-audience" style={{ fontSize: '11px' }}>Expected Audience</label>
                    <input
                      type="text"
                      id="auth-jwt-audience"
                      placeholder="mcg-gateway"
                      value={authExtJwtAudience}
                      onChange={(e) => setAuthExtJwtAudience(e.target.value)}
                      style={{ fontSize: '12px' }}
                    />
                  </div>
                  <div className="form-group" style={{ margin: 0 }}>
                    <label htmlFor="auth-jwt-issuer" style={{ fontSize: '11px' }}>Expected Issuer (Optional)</label>
                    <input
                      type="text"
                      id="auth-jwt-issuer"
                      placeholder="https://idp.corp.local"
                      value={authExtJwtIssuer}
                      onChange={(e) => setAuthExtJwtIssuer(e.target.value)}
                      style={{ fontSize: '12px' }}
                    />
                  </div>
                </div>
              )}
            </div>
          </div>
          <div style={{ marginTop: '15px', textAlign: 'right' }}>
            <button type="submit" id="btn-save-auth" className="btn btn-primary btn-sm">
              <i className="fa-solid fa-floppy-disk"></i> Save Auth Config
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
