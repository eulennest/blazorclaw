namespace BlazorClaw.Core.Services
{
    public interface ICronJobService
    {
        void ForceExecute();
        Task ExecuteNow(Guid cronJobId);
    }
}
