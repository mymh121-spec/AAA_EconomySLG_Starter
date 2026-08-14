using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Application.Platform
{
    [Flags]
    public enum HivePlatformCapability
    {
        None = 0,
        Authentication = 1 << 0,
        Matchmaking = 1 << 1,
        Achievements = 1 << 2,
        CloudSave = 1 << 3,
        Analytics = 1 << 4,
        PushNotifications = 1 << 5
    }

    public readonly struct HivePlatformResult
    {
        public bool IsAvailable { get; }
        public bool Succeeded { get; }
        public string Message { get; }
        public string Payload { get; }

        public HivePlatformResult(
            bool isAvailable,
            bool succeeded,
            string message,
            string payload = "")
        {
            IsAvailable = isAvailable;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Payload = payload ?? string.Empty;
        }

        public static HivePlatformResult Unavailable(string message) =>
            new HivePlatformResult(false, false, message);
    }

    /// <summary>
    /// Optional boundary for future HIVE platform services. The game does not
    /// call this interface in the release MVP, so an SDK is never required.
    /// </summary>
    public interface IHivePlatformExtension : IDisposable
    {
        string ProviderName { get; }
        bool IsAvailable { get; }
        HivePlatformCapability Capabilities { get; }

        Task<HivePlatformResult> InitializeAsync(
            CancellationToken cancellationToken);
        Task<HivePlatformResult> AuthenticateAsync(
            CancellationToken cancellationToken);
        Task<HivePlatformResult> UnlockAchievementAsync(
            string achievementId,
            CancellationToken cancellationToken);
        Task<HivePlatformResult> SaveCloudDataAsync(
            string slotId,
            string payload,
            CancellationToken cancellationToken);
        Task<HivePlatformResult> LoadCloudDataAsync(
            string slotId,
            CancellationToken cancellationToken);
    }

    public static class HivePlatformExtensionSlot
    {
        private static readonly IHivePlatformExtension Disabled =
            new DisabledHivePlatformExtension();
        private static IHivePlatformExtension _current = Disabled;

        public static IHivePlatformExtension Current => _current;
        public static bool HasActiveExtension =>
            _current != null && _current.IsAvailable;

        public static void Register(IHivePlatformExtension extension)
        {
            _current = extension ??
                throw new ArgumentNullException(nameof(extension));
        }

        public static void ResetToDisabled()
        {
            _current = Disabled;
        }
    }

    internal sealed class DisabledHivePlatformExtension :
        IHivePlatformExtension
    {
        private const string DisabledMessage =
            "HIVE 플랫폼 확장 슬롯은 준비되어 있지만 현재 게임에서는 비활성 상태입니다.";

        public string ProviderName => "HIVE Platform (Disabled)";
        public bool IsAvailable => false;
        public HivePlatformCapability Capabilities =>
            HivePlatformCapability.None;

        public Task<HivePlatformResult> InitializeAsync(
            CancellationToken cancellationToken) => DisabledResult(
                cancellationToken);

        public Task<HivePlatformResult> AuthenticateAsync(
            CancellationToken cancellationToken) => DisabledResult(
                cancellationToken);

        public Task<HivePlatformResult> UnlockAchievementAsync(
            string achievementId,
            CancellationToken cancellationToken) => DisabledResult(
                cancellationToken);

        public Task<HivePlatformResult> SaveCloudDataAsync(
            string slotId,
            string payload,
            CancellationToken cancellationToken) => DisabledResult(
                cancellationToken);

        public Task<HivePlatformResult> LoadCloudDataAsync(
            string slotId,
            CancellationToken cancellationToken) => DisabledResult(
                cancellationToken);

        public void Dispose()
        {
        }

        private static Task<HivePlatformResult> DisabledResult(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                HivePlatformResult.Unavailable(DisabledMessage));
        }
    }
}
