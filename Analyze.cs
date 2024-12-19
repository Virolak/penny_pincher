#region imports
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using System.Drawing;
    using QuantConnect;
    using QuantConnect.Algorithm.Framework;
    using QuantConnect.Algorithm.Framework.Selection;
    using QuantConnect.Algorithm.Framework.Alphas;
    using QuantConnect.Algorithm.Framework.Portfolio;
    using QuantConnect.Algorithm.Framework.Execution;
    using QuantConnect.Algorithm.Framework.Risk;
    using QuantConnect.Parameters;
    using QuantConnect.Benchmarks;
    using QuantConnect.Brokerages;
    using QuantConnect.Util;
    using QuantConnect.Interfaces;
    using QuantConnect.Algorithm;
    using QuantConnect.Indicators;
    using QuantConnect.Data;
    using QuantConnect.Data.Consolidators;
    using QuantConnect.Data.Custom;
    using QuantConnect.DataSource;
    using QuantConnect.Data.Fundamental;
    using QuantConnect.Data.Market;
    using QuantConnect.Data.UniverseSelection;
    using QuantConnect.Notifications;
    using QuantConnect.Orders;
    using QuantConnect.Orders.Fees;
    using QuantConnect.Orders.Fills;
    using QuantConnect.Orders.Slippage;
    using QuantConnect.Scheduling;
    using QuantConnect.Securities;
    using QuantConnect.Securities.Equity;
    using QuantConnect.Securities.Future;
    using QuantConnect.Securities.Option;
    using QuantConnect.Securities.Positions;
    using QuantConnect.Securities.Forex;
    using QuantConnect.Securities.Crypto;
    using QuantConnect.Securities.Interfaces;
    using QuantConnect.Storage;
    using QCAlgorithmFramework = QuantConnect.Algorithm.QCAlgorithm;
    using QCAlgorithmFrameworkBridge = QuantConnect.Algorithm.QCAlgorithm;
#endregion

// Crow namespace
namespace Crow {

    // Analyzes an asset based on history
    public class Analyze {

        private enum Position {Top, Bottom, None};

        // Get Swerve
        public static int getSwerve(decimal price, List<TradeBar> tradebars, float magnitude) {

            if (tradebars.Count <= 0) return 0;

            int swerve = 0;
            Position position = Position.None;

            // Positions
            if (tradebars[0].Close > price) position = Position.Top;
            if (tradebars[0].Close < price) position = Position.Bottom;

            // Check how often price swerves around average
            for (int i = 0; i < tradebars.Count; i++) {

                // Check if swerved past top
                if (tradebars[i].Close > price + price * (decimal)(magnitude / 100) && position == Position.Bottom) {
                    position = Position.Top;
                    swerve += 1;
                } 

                // Check if swerved past bottom
                else if (tradebars[i].Close < price - price * (decimal)(magnitude / 100) && position == Position.Top) {
                    position = Position.Bottom;
                    swerve += 1;
                }
            }

            return swerve;
        }

        // How many steps up
        public static int getSteps(List<TradeBar> tradebars, float magnitude) {

            // Exit if no history
            if (tradebars.Count <= 0) return 0;
            int steps = 0;

            // Set first step
            decimal firstStep = tradebars[0].Close;
            decimal lastStep = firstStep;
            magnitude = (float)(firstStep * (decimal)(magnitude / 100));

            foreach (TradeBar bar in tradebars) {

                if (bar.Close >= lastStep + (decimal)magnitude) {
                    steps += 1;
                    lastStep = bar.Close;
                }
            }

            return steps;

        }

         // How many steps up
        public static int getStepsDown(List<TradeBar> tradebars, float magnitude) {

            // Exit if no history
            if (tradebars.Count <= 0) return 0;
            int steps = 0;

            // Set first step
            decimal firstStep = tradebars[tradebars.Count - 1].Close;
            decimal lastStep = firstStep;
            magnitude = (float)(firstStep * (decimal)(magnitude / 100));

            foreach (TradeBar bar in tradebars) {

                if (bar.Close <= lastStep - (decimal)magnitude) {
                    steps += 1;
                    lastStep = bar.Close;
                }
            }

            return steps;

        }
    }
}
