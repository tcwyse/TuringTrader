﻿//==============================================================================
// Project:     Trading Solutions
// Description: Broker client for Interactive Brokers
// Description: Proprietary broker client for Interactive Brokers
// History:     2018x18, FUB, created
//------------------------------------------------------------------------------
// Copyright:   (c) 2017-2018, Bertram Solutions LLC
//              http://www.bertram.solutions
// License:     this code is licensed under GPL-3.0-or-later
//==============================================================================

#region libraries
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IBApi;
using Microsoft.Win32;
#endregion

/*

usage example

    //----- launch and connect to TWS
    BrokerClientIB client = new BrokerClientIB();
    client.Connect("user", "password");

    //----- list accounts, net liquidation value, and account positions
    var aaa = client.ManagedAcccounts;
    foreach (var a in aaa)
    {
        Console.WriteLine("{0} = {1:C2}",
            a, client.AccountSummary(a)["NetLiquidation"]);

        var ppp = client.Positions(a);
        foreach (var p in ppp)
        {
            Console.WriteLine("{0} = {1}", p.Key.Symbol, p.Value);
        }
    }

    //----- retrieve option chain and quotes
    HashSet<InstrumentInfo> info = client.ContractDetails("XSP", "OPT");
    int n = 0;
    foreach (InstrumentInfo i in info)
    {
        Console.WriteLine("'{0}': '{1}', '{2}', '{3}', '{4}': {5:C2} - {6:C2}", 
            i.Details.Summary.LocalSymbol,
            i.Details.Summary.Symbol,
            i.Details.Summary.LastTradeDateOrContractMonth,
            i.Details.Summary.Right,
            i.Details.Summary.Strike,
            i.Bid, i.Ask);

        n++;
        if (n >= 10)
            break;
    }

    //----- close connection
    client.Disconnect();

*/
namespace FUB_TradingSim
{
    public class BrokerClientIB : BrokerClientIBBase
    {
        public class InstrumentInfo
        {
            #region internal data
            private BrokerClientIB _client;
            private double? _bid = null;
            private double? _ask = null;
            private double? _last = null;
            public double? _open = null;
            public double? _high = null;
            public double? _low = null;
            public double? _close = null;
            private int? _bidSize = null;
            private int? _askSize = null;
            private int? _lastSize = null;
            private int? _volume = null;
            #endregion
            #region internal helpers
            private void GetQuoteValue(Func<bool> check)
            {
                if (check())
                    return;

                AutoResetEvent sync = new AutoResetEvent(false);
                _client.RequestMarketData(this, sync);
                do
                {
                    sync.WaitOne();
                    if (check())
                        break;
                } while (true);

                _client.CancelMarketData(this);
            }
            #endregion

            #region public InstrumentInfo(BrokerClientIB client, ContractDetails details)
            public InstrumentInfo(BrokerClientIB client, ContractDetails details)
            {
                _client = client;
                Details = details;
            }
            #endregion

            public readonly ContractDetails Details;

            #region public double Bid
            public double Bid
            {
                get
                {
                    GetQuoteValue(() => _bid != null && _ask != null);
                    return (double)_bid;
                }
                set
                {
                    _bid = value;
                }
            }
            #endregion
            #region public double Ask
            public double Ask
            {
                get
                {
                    GetQuoteValue(() => _bid != null && _ask != null);
                    return (double)_ask;
                }
                set
                {
                    _ask = value;
                }
            }
            #endregion
            #region public double Last
            public double Last
            {
                get
                {
                    GetQuoteValue(() => _last != null);
                    return (double)_last;
                }
                set
                {
                    _last = value;
                }
            }
            #endregion
            #region public double Open
            public double Open
            {
                get
                {
                    GetQuoteValue(() => _open != null);
                    return (double)_open;
                }
                set
                {
                    _open = value;
                }
            }
            #endregion
            #region public double High
            public double High
            {
                get
                {
                    GetQuoteValue(() => _high != null);
                    return (double)_high;
                }
                set
                {
                    _high = value;
                }
            }
            #endregion
            #region public double Low
            public double Low
            {
                get
                {
                    GetQuoteValue(() => _low != null);
                    return (double)_low;
                }
                set
                {
                    _low = value;
                }
            }
            #endregion
            #region public double Close
            public double Close
            {
                get
                {
                    GetQuoteValue(() => _close != null);
                    return (double)_close;
                }
                set
                {
                    _close = value;
                }
            }
            #endregion

            #region public int BidSize
            public int BidSize
            {
                get
                {
                    GetQuoteValue(() => _bidSize != null);
                    return (int)_bidSize;
                }
                set
                {
                    _bidSize = value;
                }
            }
            #endregion
            #region public int AskSize
            public int AskSize
            {
                get
                {
                    GetQuoteValue(() => _askSize != null);
                    return (int)_askSize;
                }
                set
                {
                    _askSize = value;
                }
            }
            #endregion
            #region public int LastSize
            public int LastSize
            {
                get
                {
                    GetQuoteValue(() => _lastSize != null);
                    return (int)_lastSize;
                }
                set
                {
                    _lastSize = value;
                }
            }
            #endregion
            #region public int Volume
            public int Volume
            {
                get
                {
                    GetQuoteValue(() => _volume != null);
                    return (int)_volume;
                }
                set
                {
                    _volume = value;
                }
            }
            #endregion

            #region public void RequestMarketData()
            public void RequestMarketData()
            {
                _client.RequestMarketData(this);
            }
            #endregion
            #region public void CancelMarketData()
            public void CancelMarketData()
            {
                _client.CancelMarketData(this);
            }
            #endregion
        };

        #region internal data
        private Dictionary<int, object> _asyncObject = new Dictionary<int, object>();
        private Dictionary<int, AutoResetEvent> _asyncEvent = new Dictionary<int, AutoResetEvent>();
        private string _accountFilter;
        #endregion
        #region internal helpers
        static public void LaunchTWS(string user, string password, int port = 7497)
        {
            // HKEY_CLASSES_ROOT\tws\shell\open\command\(Default) = "C:\Jts\tws.exe" "%1"
            // HKEY_CURRENT_USER\Software\Classes\tws\shell\open\command\(Default) = "C:\Jts\tws.exe" "%1"
            string shellCommand;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\tws\shell\open\command"))
            {
                shellCommand = (string)key.GetValue(null);
                string twsExe = shellCommand.Split('\"')[1];

                Process tws = new Process();
                tws.StartInfo.FileName = twsExe;
                tws.StartInfo.Arguments = string.Format("username={0} password={1}", user, password);
                //tws.StartInfo.UseShellExecute = false;
                //tws.StartInfo.RedirectStandardOutput = true;
                tws.Start();
            }

#if true
            Output.WriteLine("BrokerClientInteractiveBrokers: wait for 60 seconds");
            Thread.Sleep(TimeSpan.FromSeconds(60));
#else
            for (DateTime start = DateTime.Now; (DateTime.Now - start).TotalSeconds < 60;)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));

                using (var client = new TcpClient())
                {
                    try
                    {
                        var connection = client.BeginConnect("127.0.0.1", port, null, null);
                        var success = connection.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                        if (success)
                        {
                            client.Close();
                            return;
                        }
                        else
                        {
                            client.Close();
                            client.EndConnect(connection);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            Output.WriteLine("BrokerClientInteractiveBrokers: failed to connect to TWS on port {0}", port);
#endif
        }
        override public void nextValidId(int orderId)
        {
            NextOrderId = orderId;
        }
        #endregion
        #region public void Connect(string username, string password, int port = 7497, string ip = "127.0.0.1")
        public void Connect(string username, string password, int port = 7497, string ip = "127.0.0.1")
        {
            Console.WriteLine("Connecting...");

            try
            {
                ClientSocket.eConnect(ip, port, 0);
            }
            catch (Exception)
            {
                LaunchTWS(username, password, port);
                ClientSocket.eConnect(ip, port, 0);
            }

            EReaderSignal readerSignal = Signal;
            var reader = new EReader(ClientSocket, readerSignal);
            reader.Start();

            new Thread(() =>
            {
                while (ClientSocket.IsConnected())
                {
                    readerSignal.waitForSignal();
                    reader.processMsgs();
                }
            })
            { IsBackground = true }.Start();

            while (NextOrderId <= 0)
            {
                // once we are connected, we will have an order id > 0
            }
        }
        #endregion
        #region public void Disconnect()
        public void Disconnect()
        {
            Console.WriteLine("Disconnecting...");
            ClientSocket.eDisconnect();
        }
        #endregion

        #region error handling
        override public void error(Exception e)
        {
            Console.WriteLine("Exception thrown: " + e);
            //throw e;
        }

        override public void error(string str)
        {
        }

        //! [error]
        override public void error(int id, int errorCode, string errorMsg)
        {
        }
        #endregion
        #region market data
        override public void tickPrice(int tickerId, int field, double price, int canAutoExecute)
        {
            lock (_asyncObject)
            {
                if (!_asyncObject.ContainsKey(tickerId))
                    return;
                InstrumentInfo quote = (InstrumentInfo)_asyncObject[tickerId];

                switch (field)
                {
                    case TickType.DELAYED_ASK:
                        quote.Ask = price;
                        break;
                    case TickType.DELAYED_BID:
                        quote.Bid = price;
                        break;
                    case TickType.DELAYED_LAST:
                        quote.Last = price;
                        break;
                    case TickType.DELAYED_OPEN:
                        quote.Open = price;
                        break;
                    case TickType.DELAYED_HIGH:
                        quote.High = price;
                        break;
                    case TickType.DELAYED_LOW:
                        quote.Low = price;
                        break;
                    case TickType.DELAYED_CLOSE:
                        quote.Close = price;
                        break;
                }

                if (_asyncEvent.ContainsKey(tickerId))
                    _asyncEvent[tickerId].Set();
            }
        }
        override public void tickSize(int tickerId, int field, int size)
        {
            lock (_asyncObject)
            {
                if (!_asyncObject.ContainsKey(tickerId))
                    return;
                InstrumentInfo quote = (InstrumentInfo)_asyncObject[tickerId];

                switch (field)
                {
                    case TickType.DELAYED_BID_SIZE:
                        quote.BidSize = size;
                        break;
                    case TickType.DELAYED_ASK_SIZE:
                        quote.AskSize = size;
                        break;
                    case TickType.DELAYED_LAST_SIZE:
                        quote.LastSize = size;
                        break;
                    case TickType.DELAYED_VOLUME:
                        quote.Volume = size;
                        break;
                }

                if (_asyncEvent.ContainsKey(tickerId))
                    _asyncEvent[tickerId].Set();
            }
        }
        override public void tickString(int tickerId, int tickType, string value)
        {
            lock (_asyncObject)
            {
                if (!_asyncObject.ContainsKey(tickerId))
                    return;
                InstrumentInfo quote = (InstrumentInfo)_asyncObject[tickerId];
            }
        }
        override public void marketDataType(int reqId, int marketDataType)
        {
        }
        override public  void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
        }
        public void RequestMarketData(InstrumentInfo info, AutoResetEvent sync = null)
        {
            int infoHash = info.GetHashCode();

            lock (_asyncObject)
            {
                _asyncObject[infoHash] = info;

                if (sync != null)
                    _asyncEvent[infoHash] = sync;
            }

            ClientSocket.reqMarketDataType(3);
            ClientSocket.reqMktData(infoHash, info.Details.Summary, string.Empty, false, null);
        }
        public void CancelMarketData(InstrumentInfo info)
        {
            int infoHash = info.GetHashCode();

            ClientSocket.cancelMktData(infoHash);

            lock (_asyncObject)
            {
                _asyncObject.Remove(infoHash);

                if (_asyncEvent.ContainsKey(infoHash))
                    _asyncEvent.Remove(infoHash);
            }

        }
        #endregion
        #region contract details
        override public void contractDetails(int reqId, ContractDetails contractDetails)
        {
            lock (_asyncObject)
            {
                HashSet<InstrumentInfo> instrInfo = (HashSet<InstrumentInfo>)_asyncObject[reqId];
                instrInfo.Add(new InstrumentInfo(this, contractDetails));
            }
        }
        override public void contractDetailsEnd(int reqId)
        {
            lock (_asyncObject)
            {
                _asyncEvent[reqId].Set(); // unblock ContractDetails()
            }
        }
        public HashSet<InstrumentInfo> ContractDetails(string symbol, string secType = "STK", string exchange = "SMART", string currency = "USD")
        {
            Contract contract = new Contract
            {
                Symbol = symbol,
                SecType = secType,
                Exchange = exchange,
                Currency = currency
            };

            HashSet<InstrumentInfo> instrumentInfo = new HashSet<InstrumentInfo>();
            int contractDetailsHash = instrumentInfo.GetHashCode();
            lock (_asyncObject)
            {
                _asyncObject[contractDetailsHash] = instrumentInfo;
                _asyncEvent[contractDetailsHash] = new AutoResetEvent(false);
            }

            ClientSocket.reqContractDetails(contractDetailsHash, contract);
            _asyncEvent[contractDetailsHash].WaitOne();

            lock (_asyncObject)
            {
                _asyncObject.Remove(contractDetailsHash);
                _asyncEvent.Remove(contractDetailsHash);
            }

            return instrumentInfo;
        }
        #endregion
        #region account list
        override public void managedAccounts(string accountsList)
        {
            ManagedAcccounts.Clear();

            var accounts = accountsList.Split(',');
            foreach (var account in accounts)
                if (account.Length > 0)
                    ManagedAcccounts.Add(account);
        }
        public HashSet<string> ManagedAcccounts
        {
            // no need to request; automatically sent on connection
            get;
            private set;
        } = new HashSet<string>();
        #endregion
        #region account summary
        override public void accountSummary(int reqId, string account, string tag, string value, string currency)
        {
            lock (_asyncObject)
            {
                if (account == _accountFilter)
                {
                    Dictionary<string, double> accountSummary = (Dictionary<string, double>)_asyncObject[reqId];
                    accountSummary[tag] = Convert.ToDouble(value);
                }
            }
        }
        override public void accountSummaryEnd(int reqId)
        {
            lock (_asyncObject)
            {
                _asyncEvent[reqId].Set();
            }
        }
        public Dictionary<string, double> AccountSummary(string account)
        {
            Dictionary<string, double> accountSummary = new Dictionary<string, double>();
            int accountSummaryHash = accountSummary.GetHashCode();

            lock (_asyncObject)
            {
                _asyncObject[accountSummaryHash] = accountSummary;
                _asyncEvent[accountSummaryHash] = new AutoResetEvent(false);
            }

            _accountFilter = account;
            ClientSocket.reqAccountSummary(accountSummaryHash, "All", "TotalCashValue,NetLiquidation");
            _asyncEvent[accountSummaryHash].WaitOne();
            ClientSocket.cancelAccountSummary(accountSummaryHash);

            lock (_asyncObject)
            {
                _asyncObject.Remove(accountSummaryHash);
                _asyncEvent.Remove(accountSummaryHash);
            }

            return accountSummary;
        }
        #endregion
        #region positions
        override public void position(string account, Contract contract, double pos, double avgCost)
        {
            int reqId = 47110815;

            lock (_asyncObject)
            {
                if (account == _accountFilter)
                {
                    Dictionary<Contract, double> positions = (Dictionary<Contract, double>)_asyncObject[reqId];
                    positions[contract] = pos;
                }
            }
        }
        override public void positionEnd()
        {
            int reqId = 47110815;

            lock (_asyncObject)
            {
                _asyncEvent[reqId].Set();
            }
        }
        public Dictionary<Contract, double> Positions(string account)
        {
            Dictionary<Contract, double> positions = new Dictionary<Contract, double>();
            int positionsHash = 47110815; // reqPositions does not take a reqId

            lock (_asyncObject)
            {
                _asyncObject[positionsHash] = positions;
                _asyncEvent[positionsHash] = new AutoResetEvent(false);
            }

            _accountFilter = account;
            ClientSocket.reqPositions();
            _asyncEvent[positionsHash].WaitOne();
            ClientSocket.cancelPositions();

            lock (_asyncObject)
            {
                _asyncObject.Remove(positionsHash);
                _asyncEvent.Remove(positionsHash);
            }

            return positions;
        }
        #endregion
    }
}

//==============================================================================
// end of file