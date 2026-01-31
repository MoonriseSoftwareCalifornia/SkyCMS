using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Net;

namespace Cosmos.DynamicConfig
{
    /// <summary>
    /// Minimal IP address range/CIDR helper supporting IPv4 and IPv6.
    /// Supports parsing formats: single IP, CIDR ("192.168.0.0/24" or "2001:db8::/32"),
    /// and start-end ranges ("192.168.0.1-192.168.0.254").
    /// </summary>
    public sealed class IPAddressRange
    {
        public IPAddress Start { get; }
        public IPAddress End { get; }

        public IPAddressRange(IPAddress single)
        {
            Start = single ?? throw new ArgumentNullException(nameof(single));
            End = single;
        }

        public IPAddressRange(IPAddress start, IPAddress end)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            if (end == null) throw new ArgumentNullException(nameof(end));
            if (start.AddressFamily != end.AddressFamily) throw new ArgumentException("Start and end must be same address family");
            var si = ToBigInt(start);
            var ei = ToBigInt(end);
            if (si > ei) throw new ArgumentException("Start must be less than or equal to end");
            Start = start;
            End = end;
        }

        public static IPAddressRange Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new FormatException("Input cannot be null or whitespace");
            input = input.Trim();
            if (input.Contains("[") || input.Contains("]")) throw new FormatException("Bracketed IPv6 addresses are not supported");
            if (input.Contains("%")) throw new FormatException("IPv6 zone IDs are not supported");

            // CIDR: x.x.x.x/yy or ipv6/yy
            if (input.Contains('/'))
            {
                var parts = input.Split('/');
                if (parts.Length != 2) throw new FormatException("Invalid CIDR format");
                if (!IPAddress.TryParse(parts[0], out var network)) throw new FormatException("Invalid network address");
                if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)) throw new FormatException("Invalid prefix length");
                return CreateRangeFromCidr(network, prefix);
            }

            // Range: start-end
            if (input.Contains('-'))
            {
                var parts = input.Split('-');
                if (parts.Length != 2) throw new FormatException("Invalid range format");
                if (!IPAddress.TryParse(parts[0].Trim(), out var start)) throw new FormatException("Invalid start IP");
                if (!IPAddress.TryParse(parts[1].Trim(), out var end)) throw new FormatException("Invalid end IP");
                return new IPAddressRange(start, end);
            }

            // Single IP
            if (IPAddress.TryParse(input, out var ip))
            {
                return new IPAddressRange(ip);
            }

            throw new FormatException("Unrecognized IP range format");
        }

        public bool Contains(IPAddress ip)
        {
            if (ip == null) return false;
            if (ip.AddressFamily != Start.AddressFamily) return false;
            var i = ToBigInt(ip);
            return ToBigInt(Start) <= i && i <= ToBigInt(End);
        }

        private static IPAddressRange CreateRangeFromCidr(IPAddress network, int prefixLength)
        {
            var bytes = network.GetAddressBytes();
            var bitLength = bytes.Length * 8;
            if (prefixLength < 0 || prefixLength > bitLength) throw new FormatException("Invalid prefix length for address family");
            var networkInt = ToBigInt(network);
            var mask = (~BigInteger.Zero) << (bitLength - prefixLength);
            var startInt = networkInt & mask;
            var endInt = startInt | (~mask & ((BigInteger.One << bitLength) - 1));
            var startBytes = ToBytes(startInt, bytes.Length);
            var endBytes = ToBytes(endInt, bytes.Length);
            var startIp = new IPAddress(startBytes);
            var endIp = new IPAddress(endBytes);
            return new IPAddressRange(startIp, endIp);
        }

        private static BigInteger ToBigInt(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            var arr = new byte[bytes.Length + 1]; // add extra zero for sign
            // Reverse bytes for little-endian BigInteger
            for (int i = 0; i < bytes.Length; i++) arr[i] = bytes[bytes.Length - 1 - i];
            // arr[bytes.Length] is already 0 (sign)
            return new BigInteger(arr);
        }

        private static byte[] ToBytes(BigInteger value, int length)
        {
            var bytes = value.ToByteArray();
            // bytes are little-endian, trimmed; produce big-endian of requested length
            var result = new byte[length];
            for (int i = 0; i < length; i++)
            {
                int srcIndex = i < bytes.Length ? i : -1;
                byte b = 0;
                if (srcIndex >= 0) b = bytes[srcIndex];
                result[length - 1 - i] = b;
            }
            return result;
        }
    }
}
