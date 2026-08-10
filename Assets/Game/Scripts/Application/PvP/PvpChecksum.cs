using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.Application.PvP
{
    public static class PvpChecksum
    {
        public static string ComputeTurnPackage(
            PvpMatchId matchId,
            Game.Domain.Common.TurnNumber turn,
            IReadOnlyList<PvpCommandEnvelope> commands)
        {
            var canonical = new StringBuilder(512);
            Append(canonical, matchId.Value);
            Append(canonical, turn.Value);
            Append(canonical, commands?.Count ?? 0);

            if (commands != null)
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    PvpCommandEnvelope command = commands[i];
                    Append(canonical, command.CommandId);
                    Append(canonical, command.PlayerId.Value);
                    Append(canonical, command.CompanyId.Value);
                    Append(canonical, command.Turn.Value);
                    Append(canonical, command.Sequence);
                    Append(canonical, (int)command.Kind);
                    Append(canonical, command.Payload.RegionId.Value);
                    Append(
                        canonical,
                        command.Payload.ResourceId.HasValue
                            ? command.Payload.ResourceId.Value.Value
                            : string.Empty);
                    Append(
                        canonical,
                        command.Payload.TargetCompanyId.HasValue
                            ? command.Payload.TargetCompanyId.Value.Value
                            : string.Empty);
                    Append(canonical, command.Payload.TargetId);
                    Append(canonical, command.Payload.Quantity);
                    Append(canonical, command.Payload.LimitPrice);
                }
            }

            return ComputeSha256(canonical);
        }

        public static string ComputeClientRequest(PvpClientRequest request)
        {
            var canonical = new StringBuilder(512);
            Append(canonical, request.ProtocolVersion);
            Append(canonical, request.RequestId);
            Append(canonical, (int)request.Kind);
            Append(canonical, request.MatchId.Value);
            Append(canonical, request.PlayerId.Value);
            Append(canonical, request.ExpectedRevision);
            Append(canonical, request.CommandId);

            PvpCommandEnvelope command = request.Command;
            Append(canonical, command == null ? 0 : 1);
            if (command != null)
            {
                Append(canonical, command.CommandId);
                Append(canonical, command.MatchId.Value);
                Append(canonical, command.PlayerId.Value);
                Append(canonical, command.CompanyId.Value);
                Append(canonical, command.Turn.Value);
                Append(canonical, command.Sequence);
                Append(canonical, (int)command.Kind);
                Append(canonical, command.Payload.RegionId.Value);
                Append(
                    canonical,
                    command.Payload.ResourceId.HasValue
                        ? command.Payload.ResourceId.Value.Value
                        : string.Empty);
                Append(
                    canonical,
                    command.Payload.TargetCompanyId.HasValue
                        ? command.Payload.TargetCompanyId.Value.Value
                        : string.Empty);
                Append(canonical, command.Payload.TargetId);
                Append(canonical, command.Payload.Quantity);
                Append(canonical, command.Payload.LimitPrice);
            }

            return ComputeSha256(canonical);
        }

        private static string ComputeSha256(StringBuilder canonical)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
                hash = sha256.ComputeHash(bytes);

            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));

            return hex.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(safe.Length);
            builder.Append(':');
            builder.Append(safe);
            builder.Append('|');
        }

        private static void Append(StringBuilder builder, int value)
        {
            Append(builder, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder builder, decimal value)
        {
            Append(
                builder,
                value.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture));
        }
    }
}
