﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Jayrock.Json;
using System.Collections;
using System.Drawing;
using System.Text.RegularExpressions;

namespace TransmissionRemoteDotnet
{
    public class Torrent
    {
        private ListViewItem item;

        public ListViewItem Item
        {
            get { return item; }
        }
        private JsonObject info;

        public JsonObject Info
        {
            get { return info; }
        }
        private long updateSerial;

        public long UpdateSerial
        {
            get { return updateSerial; }
        }

        public Torrent(JsonObject info)
        {
            this.updateSerial = Program.DaemonDescriptor.UpdateSerial;
            this.info = info;
            item = new ListViewItem(this.Name);
            if (this.HasError)
            {
                item.ForeColor = Color.Red;
            }
            item.ToolTipText = item.Name;
            item.Tag = this;
            item.SubItems.Add(this.TotalSizeString);
            decimal percentage = this.StatusCode == ProtocolConstants.STATUS_CHECKING ? this.RecheckPercentage : this.Percentage;
            item.SubItems.Add(percentage.ToString() + "%");
            item.SubItems[2].Tag = percentage;
            item.SubItems.Add(this.Status);
            item.SubItems.Add((this.Seeders < 0 ? "?" : this.Seeders.ToString()) + " (" + this.PeersSendingToUs + ")");
            item.SubItems.Add((this.Leechers < 0 ? "?" : this.Leechers.ToString()) + " (" + this.PeersGettingFromUs + ")");
            item.SubItems.Add(this.StatusCode == ProtocolConstants.STATUS_DOWNLOADING && this.Percentage <= 100 ? this.DownloadRate : "");
            item.SubItems.Add(this.StatusCode == ProtocolConstants.STATUS_SEEDING || this.StatusCode == ProtocolConstants.STATUS_DOWNLOADING ? this.UploadRate : "");
            item.SubItems.Add(this.GetShortETA());
            item.SubItems.Add(this.UploadedString);
            item.SubItems.Add(this.LocalRatioString);
            item.SubItems.Add(this.Added.ToString());
            item.SubItems.Add(this.IsFinished ? "?" : "");
            item.SubItems.Add(GetFirstTracker(true));
            lock (Program.TorrentIndex)
            {
                Program.TorrentIndex[this.Hash] = this;
            }
            Add();
        }

        private delegate void AddDelegate();
        private void Add()
        {
            MainWindow form = Program.Form;
            if (form.InvokeRequired)
            {
                form.Invoke(new AddDelegate(this.Add));
            }
            else
            {
                lock (form.torrentListView)
                {
                    form.torrentListView.Items.Add(item);
                }
                lock (form.stateListBox)
                {
                    if (!form.stateListBox.Items.Contains(item.SubItems[13].Text))
                    {
                        form.stateListBox.Items.Add(item.SubItems[13].Text);
                    }
                }
                if (LocalSettingsSingleton.Instance.StartedBalloon && this.updateSerial > 1)
                {
                    Program.Form.notifyIcon.ShowBalloonTip(LocalSettingsSingleton.BALLOON_TIMEOUT, this.Name, String.Format("New torrent {0}.", this.Status.ToLower()), ToolTipIcon.Info);
                }
                LogError();
            }
        }

        private void LogError()
        {
            if (this.HasError)
            {
                List<ListViewItem> logItems = Program.LogItems;
                lock (logItems)
                {
                    if (logItems.Count > 0)
                    {
                        foreach (ListViewItem item in logItems)
                        {
                            if (item.Tag != null && this.updateSerial-(long)item.Tag < 2 && item.SubItems[1].Text.Equals(this.Name) && item.SubItems[2].Text.Equals(this.ErrorString))
                            {
                                item.Tag = this.updateSerial;
                                return;
                            }
                        }
                    }
                }
                Program.Log(this.Name, this.ErrorString, this.updateSerial);
            }
        }

        public void Show()
        {
            ListView.ListViewItemCollection itemCollection = Program.Form.torrentListView.Items;
            if (!itemCollection.Contains(item))
            {
                lock (Program.Form.torrentListView)
                {
                    if (!itemCollection.Contains(item))
                    {
                        itemCollection.Add(item);
                    }
                }
            }
        }

        public void Remove()
        {
            MainWindow form = Program.Form;
            int matchingTrackers = 0;
            ListView.ListViewItemCollection itemCollection = form.torrentListView.Items;
            if (itemCollection.Contains(item))
            {
                lock (form.torrentListView)
                {
                    if (itemCollection.Contains(item))
                    {
                        itemCollection.Remove(item);
                    }
                }
            }
            else
            {
                return;
            }
            lock (Program.TorrentIndex)
            {
                foreach (KeyValuePair<string, Torrent> pair in Program.TorrentIndex)
                {
                    if (this.item.SubItems[13].Text.Equals(pair.Value.item.SubItems[13].Text))
                    {
                        matchingTrackers++;
                    }
                }
            }
            if (matchingTrackers <= 0)
            {
                lock (form.stateListBox)
                {
                    form.stateListBox.Items.Remove(item.SubItems[13].Text);
                }
            }
        }

        public delegate void UpdateDelegate(JsonObject info);
        public void Update(JsonObject info)
        {
            MainWindow form = Program.Form;
            if (form.InvokeRequired)
            {
                form.Invoke(new UpdateDelegate(this.Update), info);
            }
            else
            {
                if (LocalSettingsSingleton.Instance.CompletedBaloon
                    && form.notifyIcon.Visible == true
                    && this.StatusCode == ProtocolConstants.STATUS_DOWNLOADING
                    && this.LeftUntilDone > 0
                    && (Toolbox.ToLong(info[ProtocolConstants.FIELD_LEFTUNTILDONE]) == 0))
                {
                    form.notifyIcon.ShowBalloonTip(LocalSettingsSingleton.BALLOON_TIMEOUT, this.Name, "This torrent has finished downloading.", ToolTipIcon.Info);
                    item.SubItems[12].Text = DateTime.Now.ToString();
                }
                this.info = info;
                item.SubItems[0].Text = this.Name;
                item.ForeColor = this.HasError ? Color.Red : SystemColors.WindowText;
                item.SubItems[1].Text = this.TotalSizeString;
                decimal percentage = this.StatusCode == ProtocolConstants.STATUS_CHECKING ? this.RecheckPercentage : this.Percentage;
                item.SubItems[2].Tag = percentage;
                item.SubItems[2].Text = percentage.ToString() + "%";
                item.SubItems[3].Text = this.Status;
                item.SubItems[4].Text = (this.Seeders < 0 ? "?" : this.Seeders.ToString()) + " (" + this.PeersSendingToUs + ")";
                item.SubItems[5].Text = (this.Leechers < 0 ? "?" : this.Leechers.ToString()) + " (" + this.PeersGettingFromUs + ")";
                item.SubItems[6].Text = this.StatusCode == ProtocolConstants.STATUS_DOWNLOADING && this.Percentage <= 100 ? this.DownloadRate : "";
                item.SubItems[7].Text = this.StatusCode == ProtocolConstants.STATUS_SEEDING || this.StatusCode == ProtocolConstants.STATUS_DOWNLOADING ? this.UploadRate : "";
                item.SubItems[8].Text = this.GetShortETA();
                item.SubItems[9].Text = this.UploadedString;
                item.SubItems[10].Text = this.LocalRatioString;
                item.SubItems[11].Text = this.Added.ToString();
                this.updateSerial = Program.DaemonDescriptor.UpdateSerial;
                LogError();
            }
        }

        public JsonArray Peers
        {
            get
            {
                return (JsonArray)info[ProtocolConstants.FIELD_PEERS];
            }
        }

        public int PieceCount
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_PIECECOUNT]);
            }
        }

        public double SeedRatioLimit
        {
            get
            {
                return Toolbox.ToDouble(info[ProtocolConstants.FIELD_SEEDRATIOLIMIT]);
            }
        }

        public bool SeedRatioMode
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_SEEDRATIOMODE]) > 0;
            }
        }

        public bool HonorsSessionLimits
        {
            get
            {
                return Toolbox.ToBool(info[ProtocolConstants.FIELD_HONORSSESSIONLIMITS]);
            }
        }

        public byte[] Pieces
        {
            get
            {
                if (info.Contains(ProtocolConstants.FIELD_PIECES))
                {
                    string pieces = (string)info[ProtocolConstants.FIELD_PIECES];
                    return Convert.FromBase64CharArray(pieces.ToCharArray(), 0, pieces.Length); ;
                }
                else
                {
                    return null;
                }
            }
        }

        public string GetFirstTracker(bool trim)
        {
            try
            {
                JsonObject tracker = (JsonObject)this.Trackers[0];
                Uri announceUrl = new Uri((string)tracker["announce"]);
                if (!trim)
                {
                    return announceUrl.Host;
                }
                else
                {
                    return Regex.Replace(Regex.Replace(Regex.Replace(announceUrl.Host, @"^tracker\.", "", RegexOptions.IgnoreCase), @"^www\.", "", RegexOptions.IgnoreCase), @"^torrent\.", "", RegexOptions.IgnoreCase);
                }
            }
            catch
            {
                return "";
            }
        }

        public int MaxConnectedPeers
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_MAXCONNECTEDPEERS]);
            }
        }

        public JsonArray Trackers
        {
            get
            {
                return (JsonArray)info[ProtocolConstants.FIELD_TRACKERS];
            }
        }

        public string Name
        {
            get
            {
                return (string)info[ProtocolConstants.FIELD_NAME];
            }
        }

        public string Hash
        {
            get
            {
                return (string)info[ProtocolConstants.FIELD_HASHSTRING];
            }
        }

        public string Status
        {
            get
            {
                switch (this.StatusCode)
                {
                    case 1:
                        return OtherStrings.WaitingToCheck;
                    case 2:
                        return OtherStrings.Checking;
                    case 4:
                        return OtherStrings.Downloading;
                    case 8:
                        return OtherStrings.Seeding;
                    case 16:
                        return OtherStrings.Paused;
                    default:
                        return "Unknown";
                }
            }
        }

        public short StatusCode
        {
            get
            {
                return Toolbox.ToShort(info[ProtocolConstants.FIELD_STATUS]);
            }
        }

        public int Id
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_ID]);
            }
        }

        public bool HasError
        {
            get
            {
                return this.ErrorString != null && !this.ErrorString.Equals("");
            }
        }

        public string ErrorString
        {
            get
            {
                return (string)info[ProtocolConstants.FIELD_ERRORSTRING];
            }
        }

        public string Creator
        {
            get
            {
                return (string)info[ProtocolConstants.FIELD_CREATOR];
            }
        }

        public string DownloadDir
        {
            get
            {
                return (string)info[ProtocolConstants.FIELD_DOWNLOADDIR];
            }
        }

        public string GetShortETA()
        {
            return GetETA(true);
        }

        public string GetLongETA()
        {
            return GetETA(false);
        }

        private string GetETA(bool small)
        {
            if (this.IsFinished)
            {
                return "";
            }
            else
            {
                double eta = Toolbox.ToDouble(info[ProtocolConstants.FIELD_ETA]);
                if (eta > 0)
                {
                    TimeSpan ts = TimeSpan.FromSeconds(eta);
                    if (small)
                    {
                        return ts.ToString();
                    }
                    else
                    {
                        return Toolbox.FormatTimespanLong(ts);
                    }
                }
                else
                {
                    return "";
                }
            }
        }

        public decimal RecheckPercentage
        {
            get
            {
                return Toolbox.ToProgress(info[ProtocolConstants.FIELD_RECHECKPROGRESS]);
            }
        }

        public decimal Percentage
        {
            get
            {
                return Toolbox.CalcPercentage(this.HaveTotal, this.TotalSize);
            }
        }

        public int Seeders
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_SEEDERS]);
            }
        }

        public int Leechers
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_LEECHERS]);
            }
        }

        public string SwarmSpeed
        {
            get
            {
                return Toolbox.GetSpeed(info[ProtocolConstants.FIELD_SWARMSPEED]);
            }
        }

        public long TotalSize
        {
            get
            {
                return Toolbox.ToLong(info[ProtocolConstants.FIELD_TOTALSIZE]);
            }
        }

        public string TotalSizeString
        {
            get
            {
                return Toolbox.GetFileSize(this.TotalSize);
            }
        }

        public DateTime Added
        {
            get
            {
                return Toolbox.DateFromEpoch(Toolbox.ToDouble(info[ProtocolConstants.FIELD_ADDEDDATE]));
            }
        }

        public int PeersSendingToUs
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_PEERSSENDINGTOUS]);
            }
        }

        public int PeersGettingFromUs
        {
            get
            {
                return Toolbox.ToInt(info[ProtocolConstants.FIELD_PEERSGETTINGFROMUS]);
            }
        }

        public string Created
        {
            get
            {
                return Toolbox.DateFromEpoch(Toolbox.ToDouble(info[ProtocolConstants.FIELD_DATECREATED])).ToString();
            }
        }

        public long Uploaded
        {
            get
            {
                return Toolbox.ToLong(info[ProtocolConstants.FIELD_UPLOADEDEVER]);
            }
        }

        public string UploadedString
        {
            get
            {
                return Toolbox.GetFileSize(this.Uploaded);
            }
        }

        public long HaveTotal
        {
            get
            {
                return Toolbox.ToLong(info[ProtocolConstants.FIELD_HAVEVALID]) + Toolbox.ToLong(info[ProtocolConstants.FIELD_HAVEUNCHECKED]);
            }
        }

        public long HaveValid
        {
            get
            {
                return Toolbox.ToLong(info[ProtocolConstants.FIELD_HAVEVALID]);
            }
        }

        public bool IsFinished
        {
            get
            {
                return this.LeftUntilDone <= 0;
            }
        }

        public long LeftUntilDone
        {
            get
            {
                return Toolbox.ToLong(info[ProtocolConstants.FIELD_LEFTUNTILDONE]);
            }
        }

        public string HaveTotalString
        {
            get
            {
                return Toolbox.GetFileSize(this.HaveTotal);
            }
        }

        public string DownloadRate
        {
            get
            {
                return Toolbox.GetSpeed(info[ProtocolConstants.FIELD_RATEDOWNLOAD]);
            }
        }

        public string UploadRate
        {
            get
            {
                return Toolbox.GetSpeed(info[ProtocolConstants.FIELD_RATEUPLOAD]);
            }
        }

        public decimal LocalRatio
        {
            get
            {
                return Toolbox.CalcRatio(this.Uploaded, this.HaveTotal);
            }
        }

        public string LocalRatioString
        {
            get
            {
                decimal ratio = this.LocalRatio;
                return ratio < 0 ? "∞" : ratio.ToString();
            }
        }

        public string Comment
        {
            get
            {
                return (string)info[ProtocolConstants.FIELD_COMMENT];
            }
        }

        /* BEGIN CONFUSION */

        public int SpeedLimitDown
        {
            get
            {
                if (info.Contains(ProtocolConstants.FIELD_UPLOADLIMIT))
                {
                    return Toolbox.ToInt(info[ProtocolConstants.FIELD_UPLOADLIMIT]);
                }
                else
                {
                    return Toolbox.ToInt(info[ProtocolConstants.FIELD_SPEEDLIMITUP]);
                }
            }
        }

        public bool SpeedLimitDownEnabled
        {
            get
            {
                if (info.Contains(ProtocolConstants.FIELD_SPEEDLIMITDOWNENABLED))
                {
                    return Toolbox.ToBool(info[ProtocolConstants.FIELD_SPEEDLIMITDOWNENABLED]);
                }
                else if (info.Contains(ProtocolConstants.FIELD_DOWNLOADLIMITED))
                {
                    return Toolbox.ToBool(info[ProtocolConstants.FIELD_DOWNLOADLIMITED]);
                }
                else
                {
                    return Toolbox.ToBool(info[ProtocolConstants.FIELD_DOWNLOADLIMITMODE]);
                }
            }
        }

        public int SpeedLimitUp
        {
            get
            {
                if (info.Contains(ProtocolConstants.FIELD_UPLOADLIMIT))
                {
                    return Toolbox.ToInt(info[ProtocolConstants.FIELD_UPLOADLIMIT]);
                }
                else
                {
                    return Toolbox.ToInt(info[ProtocolConstants.FIELD_SPEEDLIMITUP]);
                }
            }
        }

        public bool SpeedLimitUpEnabled
        {
            get
            {
                if (info.Contains(ProtocolConstants.FIELD_SPEEDLIMITUPENABLED))
                {
                    return Toolbox.ToBool(info[ProtocolConstants.FIELD_SPEEDLIMITUPENABLED]);
                }
                else if (info.Contains(ProtocolConstants.FIELD_UPLOADLIMIT))
                {
                    return Toolbox.ToBool(info[ProtocolConstants.FIELD_UPLOADLIMIT]);
                }
                else
                {
                    return Toolbox.ToBool(info[ProtocolConstants.FIELD_UPLOADLIMITMODE]);
                }
            }
        }
        /* END CONFUSION */
    }
}