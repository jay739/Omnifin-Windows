using OmnifinNative.Models;

namespace OmnifinNative.Services;

public sealed class OmnifinApiException(string message) : Exception(message);

public interface IOmnifinApiClient
{
    Uri? ServerBaseAddress { get; set; }

    Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken);

    Task<TokenResponse> LoginUserAsync(string username, string password, CancellationToken cancellationToken);

    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task<List<RespUser>> SearchUsersAsync(UserSearchRequest request, CancellationToken cancellationToken);

    Task<Dictionary<string, long>> GetWatchTimeAsync(CancellationToken cancellationToken);

    Task EnableDisableUsersAsync(IReadOnlyList<string> userIds, bool enabled, CancellationToken cancellationToken);

    Task ExtendExpiryAsync(IReadOnlyList<string> userIds, int months, int days, CancellationToken cancellationToken);

    Task DeleteUsersAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken);

    Task<List<Invite>> GetInvitesAsync(CancellationToken cancellationToken);

    Task GenerateInviteAsync(GenerateInviteRequest request, CancellationToken cancellationToken);

    Task DeleteInviteAsync(string code, CancellationToken cancellationToken);

    Task<ProfilesResponse> GetProfilesAsync(CancellationToken cancellationToken);

    Task CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken);

    Task DeleteProfileAsync(string name, CancellationToken cancellationToken);

    Task SetDefaultProfileAsync(string name, CancellationToken cancellationToken);

    Task<GetActivitiesResponse> GetActivitiesAsync(SearchActivitiesRequest request, CancellationToken cancellationToken);

    Task AnnounceAsync(AnnouncementRequest request, CancellationToken cancellationToken);

    Task<string> GetLogsAsync(CancellationToken cancellationToken);

    Task<List<TaskInfo>> GetTasksAsync(CancellationToken cancellationToken);

    Task RunTaskAsync(string taskUrl, CancellationToken cancellationToken);

    Task RestartServerAsync(CancellationToken cancellationToken);

    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task ModifyEmailsAsync(Dictionary<string, string> userEmails, CancellationToken cancellationToken);

    Task ModifyLabelsAsync(Dictionary<string, string> userLabels, CancellationToken cancellationToken);

    Task SetAccountsAdminAsync(Dictionary<string, bool> admins, CancellationToken cancellationToken);

    Task ApplySettingsAsync(UserSettingsRequest request, CancellationToken cancellationToken);

    Task<List<BackupInfo>> GetBackupsAsync(CancellationToken cancellationToken);

    Task CreateBackupAsync(CancellationToken cancellationToken);

    Task RestoreBackupAsync(string filename, CancellationToken cancellationToken);

    Task<Models.GetServerConfigResponse> GetConfigAsync(CancellationToken cancellationToken);

    Task SaveConfigAsync(Dictionary<string, Dictionary<string, string>> config, CancellationToken cancellationToken);

    Task<RespUser> GetMyDetailsAsync(CancellationToken cancellationToken);

    Task UpdateMyEmailAsync(string email, CancellationToken cancellationToken);

    Task ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task UpdateMyContactMethodsAsync(bool email, bool telegram, bool discord, bool matrix, CancellationToken cancellationToken);
}
