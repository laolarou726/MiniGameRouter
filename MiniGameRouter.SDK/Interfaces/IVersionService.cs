namespace MiniGameRouter.SDK.Interfaces;

public interface IVersionService
{
    Task<string> GetApiVersionAsync();
}