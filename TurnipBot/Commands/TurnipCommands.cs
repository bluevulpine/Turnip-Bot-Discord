﻿using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TurnipBot.DataAccess;
using TurnipBot.Models;
using TurnipBot.Services;

namespace TurnipBot.Commands
{
    public class TurnipCommands
    {
        private readonly TurnipCalculationService _turnipCalculationService;
        private readonly TurnipRepository _turnipRepository;

        public TurnipCommands()
        {
            _turnipCalculationService = new TurnipCalculationService();
            _turnipRepository = new TurnipRepository();
        }

        [Command("prices"), Aliases("urls", "list", "list_prices", "predictions", "all_prices", "all_predictions", "list_predictions")]
        [Description("Get current turnip predictions for this week")]
        public async Task Prices(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            List<TurnipInfo> entries = _turnipRepository.GetAllTurnipsTableEntries();
            StringBuilder sb = new StringBuilder();

            if (entries.Count == 0)
            {
                sb.AppendLine("Couldn't find any prices. Get on it, nerds!");
            }
            else
            {
                foreach (TurnipInfo entry in entries)
                {
                    sb.AppendLine($"{entry.Name}: {UriConstructorService.GenerateTurnipUrl(entry)}");
                }
            }

            await ctx.RespondAsync(sb.ToString());
        }

        [Command("buy"), Aliases("buy_price", "add_buy", "add_buy_price", "update_buy_price", "update_buy", "change_buy", "change_buy_price")]
        [Description("Adds or updates the turnip purchasing price from Sunday")]
        public async Task BuyPrice(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();
            string response;

            int price = Convert.ToInt32(ctx.RawArgumentString);

            if (_turnipCalculationService.AddOrUpdateBuyPriceInTable(Convert.ToInt32(ctx.Member.Discriminator), ctx.Member.DisplayName, price))
            {
                response = $"Recorded {ctx.Member.DisplayName}'s buy price as {price} bells.";
            }
            else
            {
                response = "Could not record the price. It's probably Owen's fault.";
            }

            await ctx.RespondAsync(response);
        }

        [Command("pattern"), Aliases("add_pattern", "update_pattern", "change_pattern")]
        [Description("Set the pattern from last week's turnip sales. Defaults to 'Unknown'")]
        public async Task Pattern(CommandContext ctx, string patternString)
        {
            await ctx.TriggerTypingAsync();
            string response;
            bool success;

            if (Enum.TryParse(patternString, true, out PatternEnum pattern))
                success = _turnipCalculationService.AddPatternToRecord(Convert.ToInt32(ctx.User.Discriminator), ctx.Member.DisplayName, pattern);
            else
                success = false;

            if (success)
                response = $"Successfully added pattern {pattern}.";
            else
                response = "Pattern not recognized. Values accepted: 'Unknown', 'Decreasing', 'LargeSpike', 'SmallSpike', 'Fluctuating'";

            await ctx.RespondAsync(response);
        }

        [Command("first"), Aliases("first_flag", "add_first", "add_first_flag", "update_first", "update_first_flag", "firstTime", "first-time")]
        [Description("Add or remove the 'first time' flag for the prediction")]
        public async Task FirstTime(CommandContext ctx, bool firstTime)
        {
            await ctx.TriggerTypingAsync();
            string response;

            if (_turnipCalculationService.AddFirstTimeToRecord(Convert.ToInt32(ctx.User.Discriminator), ctx.Member.DisplayName, firstTime))
            {
                response = $"Recorded {ctx.Member.DisplayName}'s first time flag as {firstTime}.";
            }
            else
            {
                response = "Could not record the first time flag. It's probably Owen's fault.";
            }

            await ctx.RespondAsync(response);
        }

        [Command("delete"), Aliases("delete_all", "remove", "remove_all", "clear", "clear_table")]
        [Description("Delete everything from the turnips table")]
        [Hidden]
        public async Task Delete(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();
            string response;

            await ctx.RespondAsync("Are you sure you want to delete everything in the turnips table?");

            var interactivityModule = ctx.Client.GetInteractivityModule();

            var message = await interactivityModule.WaitForMessageAsync(m => m.Content.Contains("Yes", StringComparison.InvariantCultureIgnoreCase) || m.Content.Contains("No", StringComparison.InvariantCultureIgnoreCase), TimeSpan.FromSeconds(10));

            if (message.Message.Content.Equals("Yes", StringComparison.InvariantCultureIgnoreCase))
            {
                _turnipRepository.DeleteAllTurnipTableEntries();
                response = "Successfully deleted everything.";
            }
            else
            {
                response = "Nothing has been deleted.";
            }

            await ctx.RespondAsync(response);
        }
    }

    [Group("sell", CanInvokeWithoutSubcommand = true)]
    public class SellCommands
    {
        private readonly TurnipCalculationService _turnipCalculationService;

        public SellCommands()
        {
            _turnipCalculationService = new TurnipCalculationService();
        }

        public async Task ExecuteGroupAsync(CommandContext ctx, int price)
        {
            await ctx.TriggerTypingAsync();
            string response;
            DateTime currentDate = DateTimeOffsetter.ToUSCentralTime(DateTime.Now).DateTime;
            string periodOfDay = currentDate.Hour < 12 ? "morning" : "afternoon";

            try
            {
                if (_turnipCalculationService.AddOrUpdateSellPriceInDB(Convert.ToInt32(ctx.Member.Discriminator), ctx.Member.DisplayName, price))
                {
                    response = $"Recorded {ctx.Member.DisplayName}'s sell price for {currentDate.DayOfWeek} {periodOfDay} as {price} bells.";
                }
                else
                {
                    response = "Couldn't record price. It's probably Owen's fault.";
                }
            }
            catch (Exception)
            {
                response = "Crashed updating your price. What did you do??";
            }

            await ctx.RespondAsync(response);
        }

        [Command("date"), Aliases("day", "past")]
        public async Task SellWithDate(CommandContext ctx, int price, string dayOfWeekString, string timeOfDayString)
        {
            await ctx.TriggerTypingAsync();
            string[] morningOptions = new string[] { "morning", "m", "morn" };
            string[] eveningOptions = new string[] { "evening", "e", "eve", "afternoon", "a", "after" };
            bool success;
            string response;

            DateTime dateOfUpdate = DateTimeOffsetter.ToUSCentralTime(DateTimeOffset.Now).DateTime;
            success = Enum.TryParse(dayOfWeekString, true, out DayOfWeek dayOfWeek);
            if (success)
            {
                if (dayOfWeek > dateOfUpdate.DayOfWeek)
                {
                    success = false;
                }
                else 
                {
                    while (dateOfUpdate.DayOfWeek != dayOfWeek) //Keep going back in days until we have the correct day of week
                    {
                        dateOfUpdate = dateOfUpdate.AddDays(-1);
                    }
                }
            }

            if (success && morningOptions.Any(o => o.Equals(timeOfDayString, StringComparison.InvariantCultureIgnoreCase))) //Morning update
            {
                success = _turnipCalculationService.AddOrUpdateSellPriceInDB(Convert.ToInt32(ctx.Member.Discriminator), ctx.Member.DisplayName, price, dayOfWeek, true);
            }
            else if (success && eveningOptions.Any(o => o.Equals(timeOfDayString, StringComparison.InvariantCultureIgnoreCase))) //Evening update
            {
                success = _turnipCalculationService.AddOrUpdateSellPriceInDB(Convert.ToInt32(ctx.Member.Discriminator), ctx.Member.DisplayName, price, dayOfWeek, false);
            }
            else
            {
                success = false;
            }

            if (success)
            {
                response = $"Successfully updated {dayOfWeek.ToString()} {timeOfDayString}'s price to {price} bells.";
            }
            else
            {
                response = "There was an issue updating the sell price. Try again or blame Owen.";
            }

            await ctx.RespondAsync(response);
        }

        [Command("morning")]
        public async Task SellMorning(CommandContext ctx, int price)
        {
            await ctx.TriggerTypingAsync();
            string response;
            DateTime currentDate = DateTimeOffsetter.ToUSCentralTime(DateTime.Now).DateTime;

            try
            {
                if (_turnipCalculationService.AddOrUpdateSellPriceInDB(Convert.ToInt32(ctx.Member.Discriminator), ctx.Member.DisplayName, price, currentDate.DayOfWeek, true))
                {
                    response = $"Recorded {ctx.Member.DisplayName}'s sell price for {currentDate.DayOfWeek} morning as {price} bells.";
                }
                else
                {
                    response = "Couldn't record price. It's probably Owen's fault.";
                }
            }
            catch (Exception)
            {
                response = "Crashed updating your price. What did you do??";
            }

            await ctx.RespondAsync(response);
        }

        [Command("afternoon")]
        [Aliases("evening")]
        public async Task SellAfternoon(CommandContext ctx, int price)
        {
            await ctx.TriggerTypingAsync();
            string response;
            DateTime currentDate = DateTimeOffsetter.ToUSCentralTime(DateTime.Now).DateTime;

            try
            {
                if (_turnipCalculationService.AddOrUpdateSellPriceInDB(Convert.ToInt32(ctx.Member.Discriminator), ctx.Member.DisplayName, price, currentDate.DayOfWeek, false))
                {
                    response = $"Recorded {ctx.Member.DisplayName}'s sell price for {currentDate.DayOfWeek} afternoon as {price} bells.";
                }
                else
                {
                    response = "Couldn't record price. It's probably Owen's fault.";
                }
            }
            catch (Exception)
            {
                response = "Crashed updating your price. What did you do??";
            }

            await ctx.RespondAsync(response);
        }
    }
}
