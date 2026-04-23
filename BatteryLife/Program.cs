using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using Windows.Devices.Power;

namespace BatteryLife
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            NotifyIcon icon = new NotifyIcon();
            IntPtr hicon = Properties.Resources.battery_32.GetHicon();
            icon.Icon = Icon.FromHandle(hicon);
            icon.Visible = true;
            icon.BalloonTipText = "";
            icon.BalloonTipTitle = "Battery information";

            DateTime dt = DateTime.Now;
            ConsumptionCounter counter = new ConsumptionCounter();
            icon.MouseClick += (sender, e) => Icon_MouseClick(sender, e, counter);

            //Application.Run(new Form1());
        }

        private static async void Icon_MouseClick(object? sender, MouseEventArgs e, ConsumptionCounter counter)
        {
            if (e.Button == MouseButtons.Left)
            {
                string text = "";


                if (counter.Status != Windows.System.Power.BatteryStatus.NotPresent)
                {

                    double zdravi = Math.Round((double)counter.Kapacita / (double)counter.Design * 100d, 1, MidpointRounding.AwayFromZero);
                    double nabiti = Math.Round((double)counter.Zbyva / (double)counter.Kapacita * 100d, 1, MidpointRounding.AwayFromZero);


                    text = "Originální kapacita: " + counter.Design + " mWh.\r\n";
                    text += "Aktuální kapacita: " + counter.Kapacita + " mWh (" + zdravi + " %).\r\n";
                    text += "Zbývající kapacita: " + counter.Zbyva + " mWh (" + nabiti + " %).\r\n";

                    if (counter.Status == Windows.System.Power.BatteryStatus.Charging)
                    {
                        //text += "Nabíjení: " + counter.Nabijeni + " mW.\r\n";
                    }
                    else if (counter.Status == Windows.System.Power.BatteryStatus.Discharging)
                    {
                        //text += "Vybíjení: " + (counter.Nabijeni) * -1 + " mW.\r\n";
                    }
                    else
                        text += "Nenabíjí.";

                    if (counter.TimeRemaining != TimeSpan.Zero && counter.TimeRemaining != TimeSpan.MaxValue && counter.Status != Windows.System.Power.BatteryStatus.Idle)
                    {
                        text += "Zbývající čas: " + counter.TimeRemaining.ToString("h\\:mm");
                    }

                }
                else { text = "Baterie není připojena."; }



                ((NotifyIcon)sender)?.ShowBalloonTip(30000, "Battery information", text, ToolTipIcon.None);
            }
            else if (e.Button == MouseButtons.Right)
            {
                counter.Stop();
                Environment.Exit(0);
            }
        }

        internal class ConsumptionCounter
        {
            private int? design;
            private int? kapacita;
            private int? zbyva;
            private int? nabijeni;
            Battery battery;
            private bool present = false;
            private Windows.System.Power.BatteryStatus status;
            private int?[] chargeHistory = new int?[30];
            private int krok = 0;
            private bool init = false;
            private TimeSpan timeRemaining;
            Thread t;
            CancellationToken token = new CancellationToken(false);

            public ConsumptionCounter()
            {
                Create();
            }

            public int? Design
            {
                get { return design; }
            }

            public int? Kapacita
            {
                get { return kapacita; }
            }

            public int? Zbyva
            {
                get { return zbyva; }
            }

            public int? Nabijeni
            {
                get { return nabijeni; }
            }

            public bool Present
            {
                get { return present; }
            }

            public Windows.System.Power.BatteryStatus Status
            {
                get { return status; }
            }

            public TimeSpan TimeRemaining
            {
                get { return timeRemaining; }
            }

            private async void Create()
            {
                var deviceInfo = await DeviceInformation.FindAllAsync(Battery.GetDeviceSelector());
                t = new Thread(new ThreadStart(() => Calculate()));
                foreach (DeviceInformation device in deviceInfo)
                {
                    try
                    {
                        // Create battery object
                        battery = await Battery.FromIdAsync(device.Id);

                        // Get report
                        var report = battery.GetReport();

                        if (report.Status != Windows.System.Power.BatteryStatus.NotPresent)
                        {
                            present = true;
                            status = report.Status;
                            design = report.DesignCapacityInMilliwattHours;
                            kapacita = report.FullChargeCapacityInMilliwattHours;
                            zbyva = report.RemainingCapacityInMilliwattHours;

                            //double zdravi = Math.Round((double)kapacita / (double)design * 100d, 1, MidpointRounding.AwayFromZero);
                            //double nabiti = Math.Round((double)zbyva / (double)kapacita * 100d, 1, MidpointRounding.AwayFromZero);

                            nabijeni = report.ChargeRateInMilliwatts;
                        }
                        else { present = false; }
                        t.Start();
                    }
                    catch (Exception ee)
                    {

                    }
                }
            }

            public void Calculate()
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    try
                    {
                        // Get report
                        var report = battery.GetReport();

                        if (report.Status != Windows.System.Power.BatteryStatus.NotPresent)
                        {
                            status = report.Status;
                            design = report.DesignCapacityInMilliwattHours;
                            kapacita = report.FullChargeCapacityInMilliwattHours;
                            zbyva = report.RemainingCapacityInMilliwattHours;
                            nabijeni = report.ChargeRateInMilliwatts;
                            chargeHistory[krok % 30] = nabijeni;

                            if (init)
                            {
                                int celkemSpotreba = 0;
                                foreach (int? i in chargeHistory)
                                {
                                    celkemSpotreba += i.Value;
                                }
                                int prumernaSpotreba = (int)Math.Round((double)celkemSpotreba / 10d, 0, MidpointRounding.AwayFromZero);

                                double cas = (double)zbyva / Math.Abs(prumernaSpotreba);
                                if (!double.IsInfinity(cas))
                                {
                                    int hodiny = (int)cas;
                                    int minuty = (int)Math.Round((cas - (double)hodiny) * 60d, 0, MidpointRounding.AwayFromZero);
                                    timeRemaining = new TimeSpan(hodiny, minuty, 0);
                                }
                                else
                                    timeRemaining = TimeSpan.MaxValue;
                            }
                        }
                        else { present = false; }
                    }
                    catch (Exception ee)
                    {

                    }
                    if (krok % 30 == 29)
                    {
                        krok = 0;
                        init = true;
                    }
                    else
                        krok++;
                }
            }

            public void Stop()
            {
                try
                {
                    if (t != null && t.ThreadState == System.Threading.ThreadState.Running)
                    {
                        token = new CancellationToken(true);
                    }
                }
                catch { }
            }
        }
    }
}