using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.HLE;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using System;
using System.Threading;

namespace Ryujinx.Ava.Ui.Applet
{
    internal class AvaHostUiHandler : IHostUiHandler
    {
        private readonly MainWindow _parent;

        public AvaHostUiHandler(MainWindow parent)
        {
            _parent = parent;
        }

        public bool DisplayMessageDialog(ControllerAppletUiArgs args)
        {
            string playerCount = args.PlayerCountMin == args.PlayerCountMax
                ? $"exactly {args.PlayerCountMin}"
                : $"{args.PlayerCountMin}-{args.PlayerCountMax}";

            string message = $"Application requests {playerCount} player(s) with:\n\n"
                             + $"TYPES: {args.SupportedStyles}\n\n"
                             + $"PLAYERS: {string.Join(", ", args.SupportedPlayers)}\n\n"
                             + (args.IsDocked ? "Docked mode set. Handheld is also invalid.\n\n" : "")
                             + "Please open Settings and reconfigure Input now or press Close.";

            return DisplayMessageDialog("Controller Applet", message);
        }

        public bool DisplayMessageDialog(string title, string message)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool okPressed = false;

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    ManualResetEvent deferEvent = new(false);

                    UserResult response = await ContentDialogHelper.ShowDeferredContentDialog(_parent, title, message, "", "Open Settings Window", "", "Close", 0xF4A3, deferEvent,
                       async  (window) =>
                        {
                            SettingsWindow settingsWindow = new SettingsWindow(_parent.VirtualFileSystem, _parent.ContentManager);

                            await settingsWindow.ShowDialog(window);
                        });

                    if (response == UserResult.Ok)
                    {
                        okPressed = true;
                    }

                    dialogCloseEvent.Set();
                }
                catch (Exception ex)
                {
                    ContentDialogHelper.CreateErrorDialog(_parent, $"Error displaying Message Dialog: {ex}");

                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            return okPressed;
        }

        public bool DisplayInputDialog(SoftwareKeyboardUiArgs args, out string userText)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool okPressed = false;
            bool error = false;
            string inputText = args.InitialText ?? "";

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    SwkbdAppletWindow swkbdDialog = new(args.HeaderText, args.SubtitleText, args.GuideText)
                    {
                        Title = "Software Keyboard", Message = inputText
                    };

                    swkbdDialog.Input.Text = inputText;
                    swkbdDialog.OkButton.Content = args.SubmitText;

                    swkbdDialog.SetInputLengthValidation(args.StringLengthMin, args.StringLengthMax);

                    await swkbdDialog.ShowDialog(_parent);

                    if (swkbdDialog.IsOkPressed)
                    {
                        inputText = swkbdDialog.Input.Text;
                        okPressed = true;
                    }
                }
                catch (Exception ex)
                {
                    error = true;
                    ContentDialogHelper.CreateErrorDialog(_parent, $"Error displaying Software Keyboard: {ex}");
                }
                finally
                {
                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            userText = error ? null : inputText;

            return error || okPressed;
        }

        public void ExecuteProgram(Switch device, ProgramSpecifyKind kind, ulong value)
        {
            device.Configuration.UserChannelPersistence.ExecuteProgram(kind, value);
            ((MainWindow)_parent).AppHost?.Exit();
        }

        public bool DisplayErrorAppletDialog(string title, string message, string[] buttons)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool showDetails = false;

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    ErrorAppletWindow msgDialog = new(_parent, buttons, message)
                    {
                        Title = title, WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    msgDialog.Width = 400;

                    object response = await msgDialog.Run();

                    if (response != null)
                    {
                        if (buttons.Length > 1)
                        {
                            if ((int)response != buttons.Length - 1)
                            {
                                showDetails = true;
                            }
                        }
                    }

                    dialogCloseEvent.Set();

                    msgDialog.Close();
                }
                catch (Exception ex)
                {
                    dialogCloseEvent.Set();
                    ContentDialogHelper.CreateErrorDialog(_parent, $"Error displaying ErrorApplet Dialog: {ex}");
                }
            });

            dialogCloseEvent.WaitOne();

            return showDetails;
        }
    }
}