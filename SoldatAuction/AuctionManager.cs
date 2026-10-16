using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SoldatAuction;

/// <summary>
/// Initial test implementation. The arm is a display-only lot; it is never added to inventory.
/// Later this manager will be started by a custom event command from the CP event.
/// </summary>
internal sealed class AuctionManager
{
    private const int BookMinimumPrice = 50_000;
    private const double RivalBudgetShare = 0.40;
    private const int BidStep = 5_000;

    private readonly IMonitor monitor;
    private int initialMoney;
    private int rivalMaximumBid;
    private int currentBid;
    private bool bookBought;

    private readonly Action onFinished;

    public AuctionManager(IMonitor monitor, Action onFinished)
    {
        this.monitor = monitor;
        this.onFinished = onFinished;
    }

    public void StartEventAuction()
{
    if (!Context.IsWorldReady)
    {
        monitor.Log("Cannot start auction: world is not ready.", LogLevel.Warn);
        onFinished();
        return;
    }

        InitializeAuction();
        ShowBookIntro();
    }

    public void StartTest()
    {
        if (!Context.IsWorldReady)
        {
            monitor.Log("Load a save before starting the auction test.", LogLevel.Warn);
            return;
        }

        InitializeAuction();
        ShowBookIntro();
    }

    private void InitializeAuction()
    {
        initialMoney = Game1.player.Money;
        rivalMaximumBid = (int)Math.Floor(initialMoney * RivalBudgetShare);
        currentBid = BookMinimumPrice;
        bookBought = false;

        monitor.Log(
            $"Auction started. Initial money={initialMoney}; " +
            $"rival max bid={rivalMaximumBid}; book minimum={BookMinimumPrice}.",
            LogLevel.Info
        );
    }

    private void ShowBookIntro()
    {
        string question =
            $"Первый лот — красная книга. Минимальная ставка: {BookMinimumPrice:N0}g. " +
            $"Предел соперника: {rivalMaximumBid:N0}g. Твоих денег: {Game1.player.Money:N0}g. Участвовать?";

        Ask(question,
            new Response("book_join", "Участвовать"),
            new Response("book_pass", "Отказаться"),
            (answer) =>
            {
                if (answer == "book_pass")
                {
                    StartArmLot("Ты отказываешься от книги. Аукцион продолжается.");
                    return;
                }

                if (Game1.player.Money < BookMinimumPrice)
                {
                    Game1.drawObjectDialogue("У тебя недостаточно денег даже для минимальной ставки за книгу.");
                    StartArmLot("Книга остаётся другому участнику.");
                    return;
                }

                currentBid = BookMinimumPrice;
                BookBidRound();
            });
    }

    private void BookBidRound()
    {
        int playerNextBid = currentBid + BidStep;
        bool rivalCanRaise = rivalMaximumBid >= playerNextBid;
        string rivalText = rivalCanRaise
            ? $"Соперник готов повысить ставку. Текущая цена: {currentBid:N0}g."
            : $"Соперник достиг своего предела ({rivalMaximumBid:N0}g). Текущая цена: {currentBid:N0}g.";

        if (Game1.player.Money < currentBid)
        {
            Game1.drawObjectDialogue("Тебе больше нечем ответить. Книга уходит сопернику.");
            StartArmLot("Торги за книгу окончены.");
            return;
        }

        Ask($"{rivalText} Твоя ставка: {currentBid:N0}g. Продолжить торги?",
            new Response("book_raise", $"Повысить до {playerNextBid:N0}g"),
            new Response("book_stop", "Сойти с торгов"),
            (answer) =>
            {
                if (answer == "book_stop")
                {
                    StartArmLot("Ты сходишь с торгов за книгу.");
                    return;
                }

                if (playerNextBid > Game1.player.Money)
                {
                    Game1.drawObjectDialogue("У тебя недостаточно денег для следующей ставки.");
                    StartArmLot("Книга уходит другому участнику.");
                    return;
                }

                // If the rival can raise to the next level, the rival answers and the round repeats.
                currentBid = playerNextBid;
                if (rivalMaximumBid >= currentBid + BidStep)
                {
                    currentBid += BidStep;
                    monitor.Log($"Rival raised the book bid to {currentBid}.", LogLevel.Trace);
                    BookBidRound();
                    return;
                }

                // The rival cannot raise further: the player wins at their current bid.
                WinBook(currentBid);
            });
    }

    private void WinBook(int price)
    {
        if (Game1.player.Money < price)
        {
            StartArmLot("Ставка оказалась недоступна. Торги переходят к следующему лоту.");
            return;
        }

        Game1.player.Money -= price;
        bookBought = true;

        Item redBook = ItemRegistry.Create("(O)lele.Soldat.JA_RedBook");
        Game1.player.addItemByMenuIfNecessary(redBook);
        Game1.playSound("gainedCoins");
        
        Game1.drawObjectDialogue($"Ты выигрываешь красную книгу за {price:N0}g. (Выдачу предмета подключим после проверки ID.)");
        monitor.Log($"Player won book for {price}. Money remaining={Game1.player.Money}.", LogLevel.Info);

        StartArmLot("На стол выставляют металлическую руку.");
    }

    private void StartArmLot(string intro)
    {
        monitor.Log($"Arm lot started. Book bought={bookBought}; money remaining={Game1.player.Money}.", LogLevel.Info);

        // No inventory object is created for the arm. This is a display-only auction lot.
        // The arm's winning price must be higher than the player's remaining money.
        int armUnaffordablePrice = Math.Max(Game1.player.Money + 1, initialMoney + 1);

        Ask($"{intro} Второй лот — металлическая рука. Цена для тебя недоступна ({armUnaffordablePrice:N0}g).",
            new Response("arm_watch", "Наблюдать за торгами"),
            new Response("arm_leave", "Отойти от стола"),
            (answer) =>
            {
                monitor.Log(
                    answer == "arm_watch"
                        ? "Player watched the arm auction."
                        : "Player left the auction table.",
                    LogLevel.Info
                );

                monitor.Log("Auction finished.", LogLevel.Info);
                onFinished();
            });
    }

    private static void Ask(string question, Response first, Response second, Action<string> callback)
    {
        Game1.currentLocation.createQuestionDialogue(
            question,
            new[] { first, second },
            (_, answer) => callback(answer)
        );
    }
}
