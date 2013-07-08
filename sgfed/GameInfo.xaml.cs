﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Diagnostics; // Debug.Assert


namespace SgfEd {
    /// <summary>
    /// Interaction logic for GameInfo.xaml
    /// </summary>
    public partial class GameInfo : Window {
        // Indicates whether ok or cancel hit
        public bool GameInfoConfirmed { get; set; }
        public Game Game { get; set; }
        // CommentChanged lets dialog inform main window may need to replace commentbox text.
        public bool CommentChanged { get; set; }

        public GameInfo (Game g) {
            InitializeComponent();
            this.CommentChanged = false;
            this.InitializeComponent();
            // Fill in dialog ...
            this.Game = g;
            Debug.Assert(g.MiscGameInfo != null);
            var props = g.MiscGameInfo;
            this.gameInfoPB.Text = g.PlayerBlack;
            this.gameInfoPW.Text = g.PlayerWhite;
            this.gameInfoBR.Text = props.ContainsKey("BR") ? props["BR"][0] : "";
            this.gameInfoWR.Text = props.ContainsKey("WR") ? props["WR"][0] : "";
            this.gameInfoBT.Text = props.ContainsKey("BT") ? props["BT"][0] : "";
            this.gameInfoWT.Text = props.ContainsKey("WT") ? props["WT"][0] : "";
            this.gameInfoHA.Text = g.Handicap.ToString();
            // We don't respond to any handicap changes at this point.  It is too complicated in terms of code   
            // changes for little value.  Users can use the New Game command, and if they have already started,   
                // then can't change the handicap anyway.   
            if (g.State == GameState.Started || g.Handicap != 0)
                this.gameInfoHA.Text += " -- use new game command to set handicap";  
            this.gameInfoHA.IsReadOnly = true;
            this.gameInfoKM.Text = g.Komi;
            this.gameInfoRU.Text = props.ContainsKey("RU") ? props["RU"][0] : "";
            this.gameInfoSZ.Text = g.Board.Size.ToString();
            // We don't respond to any handicap changes at this point.
            this.gameInfoSZ.IsReadOnly = true;
            this.gameInfoDT.Text = props.ContainsKey("DT") ? props["DT"][0] : "";
            this.gameInfoTM.Text = props.ContainsKey("TM") ? props["TM"][0] : "";
            this.gameInfoEV.Text = props.ContainsKey("EV") ? props["EV"][0] : "";
            this.gameInfoPC.Text = props.ContainsKey("PC") ? props["PC"][0] : "";
            this.gameInfoGN.Text = props.ContainsKey("GN") ? props["GN"][0] : "";
            this.gameInfoON.Text = props.ContainsKey("ON") ? props["ON"][0] : "";
            this.gameInfoRE.Text = props.ContainsKey("RE") ? props["RE"][0] : "";
            this.gameInfoAN.Text = props.ContainsKey("AN") ? props["AN"][0] : "";
            this.gameInfoGC.Text = g.Comments;

        }

        private void okButton_click (object sender, RoutedEventArgs e) {
            this.DialogResult = true;

            this.GameInfoConfirmed = true;
            // See what's changed and if we're dirty.
            var g = this.Game;
            var props = g.MiscGameInfo;
            if (this.gameInfoPB.Text != g.PlayerBlack) {
                g.PlayerBlack = this.gameInfoPB.Text;
                g.Dirty = true;
            }
            if (this.gameInfoPW.Text != g.PlayerWhite) {
                g.PlayerWhite = this.gameInfoPW.Text;
                g.Dirty = true;
            }
            if (this.gameInfoKM.Text != g.Komi) {
                g.Komi = this.gameInfoKM.Text;
                g.Dirty = true;
            }
            if (this.gameInfoGC.Text != g.Comments) {
                g.Comments = this.gameInfoGC.Text;
                g.Dirty = true;
                this.CommentChanged = true;
            }
            // black rank ... wish I had Lisp macros :-).
            if (props.ContainsKey("BR")) {
                if (this.gameInfoBR.Text != props["BR"][0]) {
                    if (this.gameInfoBR.Text == "")
                        props.Remove("BR");
                    else
                        props["BR"] = new List<string>() { this.gameInfoBR.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoBR.Text != "") {
                props["BR"] = new List<string>() { this.gameInfoBR.Text };
                g.Dirty = true;
            }
            // white rank ... wish I had Lisp macros :-).
            if (props.ContainsKey("WR")) {
                if (this.gameInfoWR.Text != props["WR"][0]) {
                    if (this.gameInfoWR.Text == "")
                        props.Remove("WR");
                    else
                        props["WR"] = new List<string>() { this.gameInfoWR.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoWR.Text != "") {
                props["WR"] = new List<string>() { this.gameInfoWR.Text };
                g.Dirty = true;
            }
            // black team ... wish I had Lisp macros :-).
            if (props.ContainsKey("BT")) {
                if (this.gameInfoBT.Text != props["BT"][0]) {
                    if (this.gameInfoBT.Text == "")
                        props.Remove("BT");
                    else
                        props["BT"] = new List<string>() { this.gameInfoBT.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoBT.Text != "") {
                props["BT"] = new List<string>() { this.gameInfoBT.Text };
                g.Dirty = true;
            }
            // white team ... wish I had Lisp macros :-).
            if (props.ContainsKey("WT")) {
                if (this.gameInfoWT.Text != props["WT"][0]) {
                    if (this.gameInfoWT.Text == "")
                        props.Remove("WT");
                    else
                        props["WT"] = new List<string>() { this.gameInfoWT.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoWT.Text != "") {
                props["WT"] = new List<string>() { this.gameInfoWT.Text };
                g.Dirty = true;
            }
            // rules ... wish I had Lisp macros :-).
            if (props.ContainsKey("RU")) {
                if (this.gameInfoRU.Text != props["RU"][0]) {
                    if (this.gameInfoRU.Text == "")
                        props.Remove("RU");
                    else
                        props["RU"] = new List<string>() { this.gameInfoRU.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoRU.Text != "") {
                props["RU"] = new List<string>() { this.gameInfoRU.Text };
                g.Dirty = true;
            }
            // date ... wish I had Lisp macros :-).
            if (props.ContainsKey("DT")) {
                if (this.gameInfoDT.Text != props["DT"][0]) {
                    if (this.gameInfoDT.Text == "")
                        props.Remove("DT");
                    else
                        props["DT"] = new List<string>() { this.gameInfoDT.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoDT.Text != "") {
                props["DT"] = new List<string>() { this.gameInfoDT.Text };
                g.Dirty = true;
            }
            // Really should confirm text is YYYY-MM-DD format, but most SGF editors probably ignore this.
            //
            // time ... wish I had Lisp macros :-).
            if (props.ContainsKey("TM")) {
                if (this.gameInfoTM.Text != props["TM"][0]) {
                    if (this.gameInfoTM.Text == "")
                        props.Remove("TM");
                    else
                        props["TM"] = new List<string>() { this.gameInfoTM.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoTM.Text != "") {
                props["TM"] = new List<string>() { this.gameInfoTM.Text };
                g.Dirty = true;
            }
            // event name ... wish I had Lisp macros :-).
            if (props.ContainsKey("EV")) {
                if (this.gameInfoEV.Text != props["EV"][0]) {
                    if (this.gameInfoEV.Text == "")
                        props.Remove("EV");
                    else
                        props["EV"] = new List<string>() { this.gameInfoEV.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoEV.Text != "") {
                props["EV"] = new List<string>() { this.gameInfoEV.Text };
                g.Dirty = true;
            }
            // place ... wish I had Lisp macros :-).
            if (props.ContainsKey("PC")) {
                if (this.gameInfoPC.Text != props["PC"][0]) {
                    if (this.gameInfoPC.Text == "")
                        props.Remove("PC");
                    else
                        props["PC"] = new List<string>() { this.gameInfoPC.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoPC.Text != "") {
                props["PC"] = new List<string>() { this.gameInfoPC.Text };
                g.Dirty = true;
            }
            // game name ... wish I had Lisp macros :-).
            if (props.ContainsKey("GN")) {
                if (this.gameInfoGN.Text != props["GN"][0]) {
                    if (this.gameInfoGN.Text == "")
                        props.Remove("GN");
                    else
                        props["GN"] = new List<string>() { this.gameInfoGN.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoGN.Text != "") {
                props["GN"] = new List<string>() { this.gameInfoGN.Text };
                g.Dirty = true;
            }
            // opening name ... wish I had Lisp macros :-).
            if (props.ContainsKey("ON")) {
                if (this.gameInfoON.Text != props["ON"][0]) {
                    if (this.gameInfoON.Text == "")
                        props.Remove("ON");
                    else
                        props["ON"] = new List<string>() { this.gameInfoON.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoON.Text != "") {
                props["ON"] = new List<string>() { this.gameInfoON.Text };
                g.Dirty = true;
            }
            // result ... wish I had Lisp macros :-).
            if (props.ContainsKey("RE")) {
                if (this.gameInfoRE.Text != props["RE"][0]) {
                    if (this.gameInfoRE.Text == "")
                        props.Remove("RE");
                    else
                        props["RE"] = new List<string>() { this.gameInfoRE.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoRE.Text != "") {
                props["RE"] = new List<string>() { this.gameInfoRE.Text };
                g.Dirty = true;
            }
            // Really should confirm text is b+2, b+2.5, b+result, etc. format, but what SGF editors look at this.
            //
            // annotator ... wish I had Lisp macros :-).
            if (props.ContainsKey("AN")) {
                if (this.gameInfoAN.Text != props["AN"][0]) {
                    if (this.gameInfoAN.Text == "")
                        props.Remove("AN");
                    else
                        props["AN"] = new List<string>() { this.gameInfoAN.Text };
                    g.Dirty = true;
                }
            }
            else if (this.gameInfoAN.Text != "") {
                props["AN"] = new List<string>() { this.gameInfoAN.Text };
                g.Dirty = true;
            }
        } //okbutton_click

    } // gameinfo class
} // namespace
