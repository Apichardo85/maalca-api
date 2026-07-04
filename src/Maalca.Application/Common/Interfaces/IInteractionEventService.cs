namespace Maalca.Application.Common.Interfaces;

public static class InteractionEventKeys
{
    public const string QrScan = "qr_scan";
    public const string CanalClick = "canal_click";
    public const string PageView = "page_view";
}

public interface IInteractionEventService
{
    Task RecordAsync(Guid affiliateId, string type, Guid? canalId);
}
