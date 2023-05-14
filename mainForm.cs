using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BackgroundMuter
{
    public partial class BackgroundMuter : Form
    {
        private readonly Dictionary<string, int> _targetId = new Dictionary<string, int>();
        private string _lastProcessName = string.Empty;
        private static readonly string[] TargetNames = {"starrail","genshinimpact"};
        private readonly RegistryKey _key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        public BackgroundMuter()
        {
            InitializeComponent();
            Init();
            ChangeMuteState(GetActiveProcessFileName().ToLower());
            StartListeningForWindowChanges();
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            Opacity = 0;
            base.OnLoad(e);
        }

        private void Init()
        {
            foreach (var targetName in TargetNames)
            {
                UpdateTargetProcessId(targetName);
            }
            SetStripAutoStart();
            SetStripVersion();
        }

        private void SetStripVersion()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                var intMajor = ApplicationDeployment.CurrentDeployment.CurrentVersion﻿.Major;
                var intMinor = ApplicationDeployment.CurrentDeployment.CurrentVersion﻿.Minor;
                var intBuild = ApplicationDeployment.CurrentDeployment.CurrentVersion﻿.Build;
                ToolStripMenuItem_version.Text = $@"Version : {intMajor}.{intMinor}.{intBuild}";
            }
            else
            {
                ToolStripMenuItem_version.Text = @"Portable Build";
            }
        }

        private void SetStripAutoStart()
        {
            ToolStripMenuItem_AutoStart.Checked = _key.GetValue(Name) != null;
        }

        private void UpdateTargetProcessId(string processName)
        {
            var targetProcessId = (from process in Process.GetProcesses()
                where process.ProcessName.ToLower() == processName && !string.IsNullOrEmpty(process.MainWindowTitle)
                select process.Id).FirstOrDefault();
            if(targetProcessId != default) _targetId[processName] = targetProcessId;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventProc lpfnWinEventProc, int idProcess, int idThread, uint dwflags);

        [DllImport("user32.dll")]
        private static extern int UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private delegate void WinEventProc(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild,
            int dwEventThread, int dwmsEventTime);

        private static string GetActiveProcessFileName()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            Process p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }

        const uint WINEVENT_OUTOFCONTEXT = 0;
        const uint EVENT_SYSTEM_FOREGROUND = 3;
        private IntPtr winHook;
        private WinEventProc listener;

        private void StartListeningForWindowChanges()
        {
            listener = EventCallback;
            //setting the window hook
            winHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, listener, 0, 0,
                WINEVENT_OUTOFCONTEXT);
        }

        private void StopListeningForWindowChanges()
        {
            UnhookWinEvent(winHook);
        }

        private void EventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, int idObject, int idChild,
            int dwEventThread, int dwmsEventTime)
        {
            ChangeMuteState(GetActiveProcessFileName().ToLower());
        }

        private void ChangeMuteState(string processName)
        {
            if(_lastProcessName != string.Empty) VolumeMixer.SetApplicationMute(_targetId[_lastProcessName], _lastProcessName != processName);
            if (!TargetNames.Contains(processName)) return;
            try
            {
                VolumeMixer.SetApplicationMute(_targetId[processName], false);
                _lastProcessName = processName;
            }
            catch
            {
                UpdateTargetProcessId(processName);
                if (_targetId[processName] != -1) ChangeMuteState(processName);
            }
        }

        private void ToolStripMenuItem_Exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void ToolStripMenuItem_AutoStart_CheckStateChanged(object sender, EventArgs e)
        {
            if (ToolStripMenuItem_AutoStart.Checked)
            {
                _key.SetValue(Name,Application.ExecutablePath);
            }
            else
            {
                try
                {
                    _key.DeleteValue(Name);
                }
                catch
                {
                    Console.WriteLine(e);
                }
            }
        }

        private void BackgroundMuter_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (var targetName in TargetNames)
            {
                VolumeMixer.SetApplicationMute(_targetId[targetName], false);
            }
        }

        private void BackgroundMuter_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopListeningForWindowChanges();
        }
    }
}
