using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Gpio;
using System.Threading.Tasks;
using System.Threading;
using Windows.Devices.Pwm;
using Windows.Foundation.Metadata;
using Microsoft.IoT.Lightning.Providers;
using Windows.Devices;




// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ComEdRateLight
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private const int LED_PIN = 5;

        private const int RED_LED_PIN = 5;
        private const int GREEN_LED_PIN = 4; //13;
        private const int BLUE_LED_PIN = 6;

        RgbLed led;

        private bool COMMON_ANODE = false;

        private enum LedState
        {
            Ultra,
            High,
            Medium,
            Low,
            Negative,
            Error
        }

        private LedState _ledState = new LedState();
        private double _currentPrice = 0.0;

        private DateTime _lastLoop;

        private GpioPin pin;
        private GpioPinValue pinValue;
        //private DispatcherTimer timer;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        private CancellationTokenSource _connectionTaskCancel = new CancellationTokenSource();
        private CancellationTokenSource _lightTaskCancel = new CancellationTokenSource();

        private Task _dequeueTask = null;
        private Task _lightLedTask = null;

        const double HIGH_PRICE = 5.0;
        const double WARN_PRICE = 4.0;
        const double ULTRA_PRICE = 10.0;


        Color[] colors = new Color[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow, Colors.Orange, Colors.Turquoise, Colors.White, Color.FromArgb(255, 120, 120, 120), Color.FromArgb(255, 50, 50, 50), Colors.Black };

        Color customYellow = Color.FromArgb(255, 255, 122, 0);

        public MainPage()
        {
            this.InitializeComponent();

            //timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(50000);
            //timer.Tick += Timer_Tick;

            Task.Run(InitLED).Wait();
            
            //InitGPIO();
            if (led != null)
            {
                //timer.Start();
                if (_dequeueTask == null)
                {
                    _dequeueTask = Task.Run(async () =>
                    {

                        await CheckRate(_connectionTaskCancel.Token);
                        //}
                    });
                }

                _ledState = LedState.Error;

                if (_lightLedTask == null)
                {
                    _lightLedTask = Task.Run(async () =>
                    {

                        await LightLED(_lightTaskCancel.Token);
                        //}
                    });
                }
            }
        }

        private async Task LightLED(CancellationToken cancel)
        {

            int ledDelay = 400;
            int ledBlinks = 3;

            while (true)
            {
                try
                {
                    Color ledColor;
                    ledDelay = 400;
                    ledBlinks = 3;

                    // Light LED
                    if (led != null)
                    {

                        led.Color = Colors.Black;
                        //led.On();

                        TimeSpan secSpan = DateTime.Now - _lastLoop;
                        if (secSpan.TotalSeconds > 200)
                            _ledState = LedState.Error;

                        System.Diagnostics.Debug.WriteLine(String.Format("LED State: {0} at time: {1}. Last price {2} seconds ago.", _ledState.ToString(), DateTime.Now.TimeOfDay, secSpan.TotalSeconds));

                        if (_ledState != LedState.Error)
                        {
                            switch (_ledState)
                            {
                                case LedState.Low:
                                    ledColor = Colors.Green;
                                    ledBlinks = Convert.ToInt32(_currentPrice);
                                    if (ledBlinks == 0)
                                        ledBlinks = 1;
                                    break;
                                case LedState.Medium:
                                    ledColor = customYellow;
                                    //ledColor = Colors.Yellow;
                                    break;
                                case LedState.High:
                                    ledColor = Colors.Red;
                                    ledBlinks = Convert.ToInt32(_currentPrice / 3);
                                    break;
                                case LedState.Ultra:
                                    ledColor = Colors.Blue;
                                    break;
                                case LedState.Negative:
                                    ledColor = Colors.Green;
                                    ledBlinks = 7;
                                    ledDelay = 200;
                                    break;
                                default:
                                    ledColor = Colors.Black;
                                    break;
                            }
                            if (ledColor != null)
                            {
                                await BlinkLed(led, ledBlinks, ledDelay, ledColor);
                            }


                        }
                        else
                        {
                            // Error
                            await BlinkLed(led, 7, 200, Colors.Red);

                        }

                        //led.Off();

                    }

                    if (cancel.IsCancellationRequested)
                    {
                        if (led != null)
                        {
                            led.Color = Colors.Black;
                            led.Off();
                            led.Dispose();
                            led = null;
                        }

                        break;
                    }
                }
                catch (Exception ex)
                {

                    System.Diagnostics.Debug.WriteLine(ex.Message);

                    try
                    {
                        await Logging.WriteDebugLog("Exception in {0}: {1}", "LightLED()", ex.Message);
                    }
                    catch (Exception exNested)
                    {
                        // Nothing to do; exception occurred during logging
                        // but eat it so loop continues
                        System.Diagnostics.Debug.WriteLine(exNested.Message);

                    }
                }

                await Task.Delay(3000, cancel);
            }
        }

        private async Task InitLED()
        {


            if (ApiInformation.IsApiContractPresent("Windows.Devices.DevicesLowLevelContract", 1))
            {
                try
                {

                    if (LightningProvider.IsLightningEnabled)
                    {
                        LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();

                        //var gpioControllers = await GpioController.GetControllersAsync(LightningGpioProvider.GetGpioProvider());
                        //var gpioController = gpioControllers[0];
                        //var redGpio = gpioController.OpenPin(BLUE_LED_PIN);
                        //redGpio.SetDriveMode(GpioPinDriveMode.InputPullUp);

                        var pwmControllers = await PwmController.GetControllersAsync(LightningPwmProvider.GetPwmProvider());
                        var pwmController = pwmControllers[1]; // the on-device controller
                        pwmController.SetDesiredFrequency(50); // try to match 50Hz


                        var pinR = pwmController.OpenPin(RED_LED_PIN);
                        pinR.Polarity = COMMON_ANODE ? PwmPulsePolarity.ActiveLow : PwmPulsePolarity.ActiveHigh;
                        pinR.Start();
                        //await Task.Delay(500);
                        pinR.SetActiveDutyCyclePercentage(1);

                        var pinB = pwmController.OpenPin(BLUE_LED_PIN);
                        pinB.Polarity = COMMON_ANODE ? PwmPulsePolarity.ActiveLow : PwmPulsePolarity.ActiveHigh;
                        pinB.Start();
                        //await Task.Delay(500);
                        pinB.SetActiveDutyCyclePercentage(1);

                        var pinG = pwmController.OpenPin(GREEN_LED_PIN);
                        pinG.Polarity = COMMON_ANODE ? PwmPulsePolarity.ActiveLow : PwmPulsePolarity.ActiveHigh;
                        pinG.Start();
                        //await Task.Delay(500);
                        pinG.SetActiveDutyCyclePercentage(1);

                        led = new RgbLed(pinR, pinG, pinB);
                        led.On();
                        led.Color = Colors.Black;
                    }

                   
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    try
                    {
                        await Logging.WriteDebugLog("Exception in {0}: {1}", "InitLED()", ex.Message);
                    }
                    catch (Exception exNested)
                    {
                        // Nothing to do; exception occurred during logging
                        // but eat it so loop continues
                        System.Diagnostics.Debug.WriteLine(exNested.Message);

                    }
                }
                //timer.Start();
            }
        }

        private void InitGPIOLED()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pin = null;
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            pin = gpio.OpenPin(LED_PIN);
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            pin.SetDriveMode(GpioPinDriveMode.Output);

            GpioStatus.Text = "GPIO pin initialized correctly.";

        }

        private async Task CheckRate(CancellationToken cancel)
        {
            while (true)
            {

                try
                {
                    var results = await RateService.GetRate(DateTime.Now);

                    var _localLedState = new LedState();
                    _localLedState = LedState.Error;

                    if (results != null && results.Count > 0)
                    {
                        var priceItem = results.First();
                        var intPrice = 0.0;


                        if (double.TryParse(priceItem.price, out intPrice))
                        {
                            System.Diagnostics.Debug.WriteLine(String.Format("Price: {0} at time: {1}", priceItem.price, priceItem.millisUTC));
                            _currentPrice = intPrice; 

                            if (intPrice >= ULTRA_PRICE)
                            {
                                _localLedState = LedState.Ultra;
                            }
                            else if (intPrice >= HIGH_PRICE)
                            {
                                _localLedState = LedState.High;
                            }
                            else if (intPrice >= WARN_PRICE)
                            {
                                _localLedState = LedState.Medium;
                            }
                            else if (intPrice <= 0 )
                            {
                                _localLedState = LedState.Negative;
                            }
                            else
                            {
                                _localLedState = LedState.Low;
                            }

                        }

                        _lastLoop = DateTime.Now;


                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(String.Format("No price record returned at time: {0}", System.DateTime.Now.TimeOfDay));
                        _localLedState = LedState.Error;

                    }

                    _ledState = _localLedState;


                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    _ledState = LedState.Error;

                    try
                    {
                        await Logging.WriteDebugLog("Exception in {0}: {1}", "CheckRate()", ex.Message);
                    }
                    catch (Exception exNested)
                    {
                        // Nothing to do; exception occurred during logging
                        // but eat it so loop continues
                        System.Diagnostics.Debug.WriteLine(exNested.Message);

                    }

                }

                if (_connectionTaskCancel.IsCancellationRequested) break;

                await Task.Delay(50000, cancel);
            }

        }

        private async Task BlinkPin(GpioPin Pin, int Blinks, int Delay)
        {
            // Store pin value so we leave the pin in the same statet it started
            var pinValue = Pin.Read();
            var notPinValue = pinValue == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High;

            for (int i = 0; i < Blinks; i++)
            {

                Pin.Write(notPinValue);

                await Task.Delay(Delay);

                Pin.Write(pinValue);

                await Task.Delay(Delay);



            }

            Pin.Write(pinValue);

        }

        private async Task BlinkLed(RgbLed Pin, int Blinks, int Delay, Color Color)
        {
            try
            {
                // Store pin value so we leave the pin in the same statet it started
                var pinStartColor = Pin.Color;

                for (int i = 0; i < Blinks; i++)
                {

                    led.Color = Color;

                    await Task.Delay(Delay);

                    led.Color = Colors.Black;

                    await Task.Delay(Delay);

                }

                Pin.Color = pinStartColor;
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine(ex.Message);

                try
                {
                    await Logging.WriteDebugLog("Exception in {0}: {1}", "BlinkLED()", ex.Message);
                }
                catch (Exception exNested)
                {
                    // Nothing to do; exception occurred during logging
                    // but eat it so loop continues
                    System.Diagnostics.Debug.WriteLine(exNested.Message);

                }
            }

        }

        private void Timer_Tick(object sender, object e)
        {

            //var results = await RateService.GetRate(DateTime.Now);

            if (pinValue == GpioPinValue.High)
            {
                pinValue = GpioPinValue.Low;
                pin.Write(pinValue);
                LED.Fill = redBrush;
            }
            else
            {
                pinValue = GpioPinValue.High;
                pin.Write(pinValue);
                LED.Fill = grayBrush;
            }
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
 
            var results = await RateService.GetRate(DateTime.Now);

            if (results != null && results.Count > 0)
            {
                var other = results.First();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_connectionTaskCancel != null)
                _connectionTaskCancel.Cancel();

            if (_lightTaskCancel != null)
                _lightTaskCancel.Cancel();

            if (led != null)
            {
                led.Off();
                led.Dispose();
                led = null;
            }
        }
    }
}
