
using StardewModdingAPI;
using StardewValley;

namespace SoldatAuction;

internal sealed class ModEntry : Mod
{
    private AuctionManager auction = null!;
    private Event? activeEvent;
    private bool auctionRunning;

    public override void Entry(IModHelper helper)
    {
        auction = new AuctionManager(Monitor, FinishAuctionEvent);

        Event.RegisterCommand("soldatAuctionTest", StartAuctionEvent);

        helper.ConsoleCommands.Add(
            "soldatauction_test",
            "Starts a test auction flow. Usage: soldatauction_test",
            (_, _) => auction.StartTest()
        );

        Monitor.Log("SoldatAuction loaded.", LogLevel.Info);
    }

    private void StartAuctionEvent(
        Event @event,
        string[] args,
        EventContext context)
    {
        // Не запускаем аукцион повторно, пока команда ожидает завершения.
        if (auctionRunning)
            return;

        auctionRunning = true;
        activeEvent = @event;

        Monitor.Log("Starting auction from event.", LogLevel.Info);
        auction.StartEventAuction();
    }

    private void FinishAuctionEvent()
    {
        if (!auctionRunning || activeEvent is null)
            return;

        activeEvent.CurrentCommand++;

        activeEvent = null;
        auctionRunning = false;

        Monitor.Log("Auction finished; event continues.", LogLevel.Info);
    }
}
