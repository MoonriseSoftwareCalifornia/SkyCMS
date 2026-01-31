# Proxy & Hostname Configuration (SkyCMS)

Last updated: 1/31/2026

This document covers configuring `ProxySettings`, how SkyCMS chooses a tenant hostname when requests pass through proxies/CDNs, security guidance, provider resources, testing tips, and example configuration snippets.

## Quick Reference

Example `appsettings.json` snippet:

```json
{
  "ProxySettings": {
    "TrustXOriginHostname": true,
    "TrustedProxyIPs": [
      "127.0.0.1",
      "10.0.0.0/8",
      "192.168.5.1-192.168.5.100"
    ]
  }
}
```

- `TrustXOriginHostname` (bool): when true, SkyCMS will consider a proxy-supplied hostname header (`x-origin-hostname`) but only if the request comes from a trusted proxy IP.
- `TrustedProxyIPs` (array of strings): accepts single IPs, CIDR notation (IPv4/IPv6), and start–end ranges.

Entries that cannot be parsed are ignored and will produce a warning in the application logs.

## How it Works

1. A request reaches your edge proxy or CDN. Some proxies add the original hostname in `x-origin-hostname` or `X-Forwarded-Host` when forwarding.
2. SkyCMS calls `IsFromTrustedProxy()` which checks `HttpContext.Connection.RemoteIpAddress` against `TrustedProxyIPs`.
3. If both `TrustXOriginHostname` is enabled and the remote IP is trusted, SkyCMS validates the header value using `GetValidHostName()` (IDN normalization, strict regex and Uri parsing).
4. If the header passes validation, SkyCMS uses that value for tenant lookup. Otherwise it falls back to `HttpContext.Request.Host.Host`.

This flow prevents accepting arbitrary host headers unless they originate from trusted proxies and pass validation.

## Security Guidance

- Do not enable `TrustXOriginHostname` without a properly populated `TrustedProxyIPs` allowlist.
- Prefer provider-managed service tags or official published IP ranges (see Provider Resources) and automate updates where possible.
- Use ASP.NET Core's `ForwardedHeadersMiddleware` (with `KnownProxies`/`KnownNetworks`) when you can, as it centralizes header handling and is battle-tested.
- Log and monitor warnings such as `Rejected malformed host name` and `Invalid entry in TrustedProxyIPs`.

### Hostname Validation

SkyCMS validates hostnames with these steps:
- IDN to ASCII conversion
- Regex-based DNS label validation
- `Uri` host parsing fallback

Malformed or suspicious hostnames are rejected and result in a warning.

## Provider Resources

Use the provider's published IP ranges to populate `TrustedProxyIPs`. Examples (verify the provider pages for latest data):

- Azure Front Door / Azure service tags
  - https://learn.microsoft.com/azure/frontdoor/
  - https://learn.microsoft.com/azure/virtual-network/service-tags-overview

- Cloudflare (IPv4 and IPv6 lists)
  - https://www.cloudflare.com/ips/

- AWS / CloudFront (JSON of ranges)
  - https://ip-ranges.amazonaws.com/ip-ranges.json
  - CloudFront docs: https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/

- Fastly public IP list
  - https://api.fastly.com/public-ip-list

- Sucuri (WAF/proxy)
  - https://sucuri.net/website-firewall/ (verify current IP list links)

If your provider publishes ASN or proprietary lists, convert them to CIDR or start–end ranges suitable for `TrustedProxyIPs`.

## Examples

- Single IP: `127.0.0.1`
- CIDR (IPv4): `10.0.0.0/8`
- CIDR (IPv6): `2001:db8::/32`
- Start–End range: `192.168.5.1-192.168.5.100`

## Testing & Troubleshooting

- Unit tests: See `Tests/DynamicConfig` for `IPAddressRange` and `DynamicConfigurationProvider` tests (includes CIDR, range, IPv4/IPv6, invalid format and fuzz tests).
- Local simulation: set `HttpContext.Connection.RemoteIpAddress` in tests, add `x-origin-hostname` header, and toggle `TrustedProxyIPs` to simulate trusted/untrusted proxies.
- Common log entries to look for:
  - `Invalid entry in TrustedProxyIPs` — an entry in your list couldn't be parsed.
  - `Rejected malformed host name: {value}` — the header value failed validation.

## Operational Notes

- `PreloadAllConnectionsAsync()` caches tenant connections; use it to reduce DB lookups on high-traffic sites but keep it up-to-date if domains change frequently.
- Large `TrustedProxyIPs` lists may affect startup parsing time; consider storing provider ranges in deployment automation and refreshing regularly.

## Appendix & Further Reading

- For Azure users: use service tags and automation to keep allowlists current.
- For Cloudflare/AWS/Fastly users: schedule periodic downloads of published lists and validate CIDR parsing.
