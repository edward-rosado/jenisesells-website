using Microsoft.AspNetCore.SignalR;

namespace RealEstateStar.Api.Hubs;

public class CmaProgressHub : Hub
{
    public Task JoinJob(string jobId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, jobId);
}
