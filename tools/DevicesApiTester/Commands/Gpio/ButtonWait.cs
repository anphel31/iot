﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using DeviceApiTester.Infrastructure;

namespace DeviceApiTester.Commands.Gpio
{
    [Verb("gpio-button-wait", HelpText = "Uses WaitForEventAsync to detect button presses, optionally showing the state on an LED.")]
    public class ButtonWait : GpioCommand, ICommandVerbAsync
    {
        /// <summary>Executes the command asynchronously.</summary>
        /// <returns>The command's exit code.</returns>
        /// <remarks>
        ///     NOTE: This test app uses the base class's <see cref="CreateGpioController"/> method to create a device.<br/>
        ///     Real-world usage would simply create an instance of <see cref="GpioController"/>:
        ///     <code>using (var gpio = new GpioController())</code>
        /// </remarks>
        public async Task<int> ExecuteAsync()
        {
            Console.WriteLine($"ButtonPin={ButtonPin}, LedPin={LedPin}, Scheme={Scheme}, PressedValue={PressedValue}, OnValue={OnValue}, Driver={Driver}");

            using (var gpio = CreateGpioController())
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Console.WriteLine($"Listening for button presses on GPIO {Enum.GetName(typeof(PinNumberingScheme), Scheme)} pin {ButtonPin} . . .");

                // This example runs until Ctrl+C (or Ctrl+Break) is pressed, so register a local function handler
                Console.CancelKeyPress += Console_CancelKeyPress;

                // Open the GPIO pin connected to the button
                gpio.OpenPin(ButtonPin);

                // Set the mode based on if input pull-up resistors are supported
                var inputMode = gpio.IsPinModeSupported(ButtonPin, PinMode.InputPullUp)
                    ? PinMode.InputPullUp
                    : PinMode.Input;
                gpio.SetPinMode(ButtonPin, inputMode);

                // Open the GPIO pin connected to the LED if one was specified
                if (LedPin >= 0)
                {
                    gpio.OpenPin(LedPin, PinMode.Output);
                    gpio.Write(LedPin, OffValue);
                }

                var bothPinEventTypes = PinEventTypes.Falling | PinEventTypes.Rising;
                WaitForEventResult waitResult;
                int count = 0;
                do
                {
                    waitResult = await gpio.WaitForEventAsync(ButtonPin, bothPinEventTypes, cancellationTokenSource.Token);
                    if (!waitResult.TimedOut)
                    {
                        var pressedOrReleased = waitResult.EventType == PressedValue
                            ? "pressed"
                            : "released";
                        Console.WriteLine($"[{count++}] Button {pressedOrReleased}: GPIO {Enum.GetName(typeof(PinNumberingScheme), Scheme)} pin number {ButtonPin}, ChangeType={waitResult.EventType}");

                        if (LedPin >= 0)
                        {
                            var ledValue = waitResult.EventType == PressedValue
                                ? OnValue
                                : OffValue;
                            gpio.Write(LedPin, ledValue);
                        }
                    }
                } while (!waitResult.TimedOut);

                gpio.ClosePin(ButtonPin);
                if (LedPin >= 0)
                {
                    gpio.ClosePin(LedPin);
                }

                Console.WriteLine("Operation cancelled. Exiting.");
                Console.OpenStandardOutput().Flush();

                return 0;

                // Local function
                void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    cancellationTokenSource.Cancel();
                    Console.CancelKeyPress -= Console_CancelKeyPress;
                }
            }
        }

        [Option('b', "button-pin", HelpText = "The GPIO pin to which the button is connected, numbered based on the --scheme argument", Required = true)]
        public int ButtonPin { get; set; }

        [Option('l', "led-pin", HelpText = "The GPIO pin which the LED is connected to (if any), numbered based on the --scheme argument", Required = false, Default = -1)]
        public int LedPin { get; set; }

        [Option('p', "pressed-value", HelpText = "The value of the GPIO pin when the button is pressed: { Rising | Falling }", Required = false, Default = PinEventTypes.Rising)]
        public PinEventTypes PressedValue { get; set; }

        [Option("on-value", HelpText = "The value that turns the LED on: { High | Low }", Required = false, Default = PinValue.High)]
        public PinValue OnValue { get; set; }

        private PinValue OffValue
        {
            get { return OnValue == PinValue.High ? PinValue.Low : PinValue.High; }
        }

        private PinEventTypes ReleasedValue
        {
            get { return PressedValue == PinEventTypes.Rising ? PinEventTypes.Falling : PinEventTypes.Rising; }
        }
    }
}
