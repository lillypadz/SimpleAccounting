// <copyright>
//     Copyright (c) Lukas Grützmacher. All rights reserved.
// </copyright>

namespace lg2de.SimpleAccounting.Reports;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Xml;
using lg2de.SimpleAccounting.Abstractions;
using lg2de.SimpleAccounting.Extensions;
using lg2de.SimpleAccounting.Model;
using lg2de.SimpleAccounting.Properties;

[SuppressMessage("ReSharper", "CommentTypo", Justification = "additional german comments")]
internal class AnnualBalanceReport : ReportBase, IAnnualBalanceReport
{
    public const string ResourceName = "AnnualBalance.xml";
    private const string FourthColumnExpression = "../columns/column[position()=4]";

    private readonly List<AccountDefinition> allAccounts;

    public AnnualBalanceReport(IXmlPrinter printer, IProjectData projectData, IClock clock)
        : base(ResourceName, printer, projectData, clock)
    {
        this.allAccounts = projectData.Storage.AllAccounts.ToList();
    }

    public enum PeriodCodes
    {
        None = 0,
        CurrentMonth = 1,
        LastMonth = 2,
        CurrentQuarter = 3,
        LastQuarter = 4
    }

    public void CreateReport(PeriodCodes period = PeriodCodes.None)
    {
        DateTime startDate = new DateTime(DateTime.Now.Year, 1,1);
        DateTime endDate = startDate.AddYears(1);
        string headerName = Resources.Header_AnnualBalance;
        var monthOfQtr = 1 - DateTime.Now.Month % 3;
        switch (period)
        {
        case PeriodCodes.CurrentMonth:
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            endDate = startDate.AddMonths(1);
            headerName = Resources.Header_CurrentMonthBalance;
            break;
        case PeriodCodes.LastMonth:
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
            endDate = startDate.AddMonths(1);
            headerName = Resources.Header_PreviousMonthBalance;
            break;
        case PeriodCodes.CurrentQuarter:
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(monthOfQtr);
            endDate = startDate.AddMonths(3);
            headerName = Resources.Header_CurrentQuarterBalance;
            break;
        case PeriodCodes.LastQuarter:
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(monthOfQtr - 3);
            endDate = startDate.AddMonths(3);
            headerName = Resources.Header_PreviousQuarterBalance;
            break;
        }
        uint sDate = (uint)((startDate.Year) * 10000 + startDate.Month * 100 + startDate.Day);
        uint eDate = (uint)(endDate.Year * 10000 + endDate.Month * 100 + endDate.Day);
        this.PreparePrintDocument(startDate, endDate.AddDays(-1), headerName);

        // income / Einnahmen
        this.ProcessIncome(out var totalIncome, sDate, eDate);

        // expense / Ausgaben
        this.ProcessExpenses(out var totalExpense, sDate, eDate);

        var balanceNode = this.PrintDocument.SelectSingleNode("//text[@ID=\"balance\"]")!;
        balanceNode.InnerText = (totalIncome + totalExpense).FormatCurrency();

        // receivables / Forderungen
        // liabilities / Verbindlichkeiten
        this.ProcessReceivablesAndLiabilities(out var totalReceivable, out var totalLiability);

        // asset / Vermögen
        this.ProcessAssets(totalReceivable, totalLiability);
    }

    private void ProcessIncome(out long totalIncome, uint startDate, uint endDate)
    {
        var dataNode = this.PrintDocument.SelectSingleNode("//table/data[@target='income']")!;
        totalIncome = 0;
        var accounts = this.allAccounts.Where(a => a.Type == AccountDefinitionType.Income);
        foreach (var account in accounts)
        {
            long balance = this.GetAccountBalance(account, true, startDate, endDate);
            if (balance == 0)
            {
                // account with balance zero is skipped
                continue;
            }

            totalIncome += balance;

            dataNode.AppendChild(this.CreateAccountBalanceNode(account, balance));
        }

        var balanceElement = dataNode.SelectSingleNode(FourthColumnExpression)!;
        balanceElement.InnerText = totalIncome.FormatCurrency();
    }

    private void ProcessExpenses(out long totalExpense, uint startDate, uint endDate)
    {
        var dataNode = this.PrintDocument.SelectSingleNode("//table/data[@target='expense']")!;
        totalExpense = 0;
        var accounts = this.allAccounts.Where(a => a.Type == AccountDefinitionType.Expense);
        foreach (var account in accounts)
        {
            long balance = this.GetAccountBalance(account, true, startDate, endDate);
            if (balance == 0)
            {
                // account with balance zero is skipped
                continue;
            }

            totalExpense += balance;

            dataNode.AppendChild(this.CreateAccountBalanceNode(account, balance));
        }

        var balanceElement = dataNode.SelectSingleNode(FourthColumnExpression)!;
        balanceElement.InnerText = totalExpense.FormatCurrency();
    }

    private void ProcessReceivablesAndLiabilities(out long totalReceivable, out long totalLiability)
    {
        var receivableNode = this.PrintDocument.SelectSingleNode("//table/data[@target='receivable']")!;
        var liabilityNode = this.PrintDocument.SelectSingleNode("//table/data[@target='liability']")!;
        totalReceivable = 0;
        totalLiability = 0;
        var accounts = this.allAccounts.Where(
            a =>
                a.Type == AccountDefinitionType.Debit || a.Type == AccountDefinitionType.Credit);
        foreach (var account in accounts)
        {
            long balance = this.GetAccountBalance(account, false, 19700101, 20991231);
            if (balance == 0)
            {
                // account with balance zero is skipped
                continue;
            }

            if (balance > 0)
            {
                totalReceivable += balance;
                receivableNode.AppendChild(this.CreateAccountBalanceNode(account, balance));
            }
            else // balance < 0
            {
                totalLiability += balance;
                liabilityNode.AppendChild(this.CreateAccountBalanceNode(account, balance));
            }
        }

        var balanceElement = receivableNode.SelectSingleNode(FourthColumnExpression)!;
        balanceElement.InnerText = totalReceivable.FormatCurrency();
        balanceElement = liabilityNode.SelectSingleNode(FourthColumnExpression)!;
        balanceElement.InnerText = totalLiability.FormatCurrency();
    }

    private void ProcessAssets(long totalReceivable, long totalLiability)
    {
        var dataNode = this.PrintDocument.SelectSingleNode("//table/data[@target='asset']")!;
        long totalAccount = 0;
        var accounts = this.allAccounts.Where(a => a.Type == AccountDefinitionType.Asset);
        foreach (var account in accounts)
        {
            long balance = this.GetAccountBalance(account, false, 19700101, 20991231);
            if (balance == 0)
            {
                continue;
            }

            totalAccount += balance;

            dataNode.AppendChild(this.CreateAccountBalanceNode(account, balance));
        }

        //if (totalReceivable > 0)
        //{
        //    dataNode.AppendChild(this.CreateBalanceNode(Resources.Word_Receivables, totalReceivable));
        //    totalAccount += totalReceivable;
        //}

        //if (totalLiability < 0)
        //{
        //    dataNode.AppendChild(this.CreateBalanceNode(Resources.Word_Liabilities, totalLiability));
        //    totalAccount += totalLiability;
        //}

        var balanceElement = dataNode.SelectSingleNode(FourthColumnExpression)!;
        balanceElement.InnerText = totalAccount.FormatCurrency();
    }

    private long GetAccountBalance(AccountDefinition account, bool creditFromDebit, uint fromDate, uint toDate)
    {
        long creditBalance = this.YearData.Booking
            .SelectMany(x => x.Credit.Where(y => (x.Date > fromDate && x.Date < toDate) && y.Account == account.ID))
            .DefaultIfEmpty().Sum(x => x?.Value ?? 0);
        long debitBalance = this.YearData.Booking
            .SelectMany(x => x.Debit.Where(y => (x.Date > fromDate && x.Date < toDate) && y.Account == account.ID))
            .DefaultIfEmpty().Sum(x => x?.Value ?? 0);

        if (creditFromDebit)
        {
            return creditBalance - debitBalance;
        }

        return debitBalance - creditBalance;
    }

    private XmlNode CreateAccountBalanceNode(AccountDefinition account, long balance)
    {
        string accountText = account.ID.ToString(CultureInfo.CurrentCulture).PadLeft(5, '0') + " " + account.Name;

        return this.CreateBalanceNode(accountText, balance);
    }

    private XmlNode CreateBalanceNode(string balanceText, long balance)
    {
        XmlNode dataLineNode = this.PrintDocument.CreateElement("tr");
        XmlNode dataItemNode = this.PrintDocument.CreateElement("td");

        dataLineNode.AppendChild(dataItemNode);

        dataItemNode = dataItemNode.Clone();
        dataItemNode.InnerText = balanceText;
        dataLineNode.AppendChild(dataItemNode);

        dataItemNode = dataItemNode.Clone();
        dataItemNode.InnerText = balance.FormatCurrency();
        dataLineNode.AppendChild(dataItemNode);

        return dataLineNode;
    }
}
