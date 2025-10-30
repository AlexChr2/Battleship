﻿#region Imports
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using Battleship.Utils;
using Battleship.Core;
using System.Media;
using Battleship.src.gui;
#endregion

namespace Battleship.Gui
{

    public partial class BattleFieldForm : KryptonForm
    {

        #region Variable Declarations
        private readonly GameManager gameManager;
        private readonly Panel[,] player1Grid;
        private readonly List<PictureBox> player1FleetPboxes;
        private readonly List<PictureBox> player2FleetPboxes;
        private readonly Dictionary<string, List<PictureBox>> enemyFirePboxes;
        private readonly static Random random = new Random();
        private bool mutexPlayerCanFire;
        private const int computerShotDelay = 1800;
        private readonly SoundPlayer shipHitted;
        private readonly SoundPlayer splash;
        #endregion

        /// <summary>
        /// Constructor Definition.
        /// </summary>
        public BattleFieldForm(GameManager gameManager, List<PictureBox> fleetPboxes)
        {
            InitializeComponent();
            this.gameManager = gameManager;

            player1Grid = new Panel[10, 10] {
                { p00, p01, p02, p03, p04, p05, p06, p07, p08, p09 },
                { p10, p11, p12, p13, p14, p15, p16, p17, p18, p19 },
                { p20, p21, p22, p23, p24, p25, p26, p27, p28, p29 },
                { p30, p31, p32, p33, p34, p35, p36, p37, p38, p39 },
                { p40, p41, p42, p43, p44, p45, p46, p47, p48, p49 },
                { p50, p51, p52, p53, p54, p55, p56, p57, p58, p59 },
                { p60, p61, p62, p63, p64, p65, p66, p67, p68, p69 },
                { p70, p71, p72, p73, p74, p75, p76, p77, p78, p79 },
                { p80, p81, p82, p83, p84, p85, p86, p87, p88, p89 },
                { p90, p91, p92, p93, p94, p95, p96, p97, p98, p99 }
            };

            enemyFirePboxes = new Dictionary<string, List<PictureBox>>
            {
                { "Carrier", new List<PictureBox>() },
                { "Battleship", new List<PictureBox>() },
                { "Cruiser", new List<PictureBox>() },
                { "Submarine", new List<PictureBox>() },
                { "Destroyer", new List<PictureBox>() }
            };

            player1FleetPboxes = fleetPboxes.ToList();
            player2FleetPboxes = fleetPboxes.ConvertAll(
                pBox => new PictureBox()
                {
                    Height = pBox.Height,
                    Width = pBox.Width,
                    Image = (Image)pBox.Image.Clone(),
                    Tag = pBox.Tag,
                    SizeMode = pBox.SizeMode,
                    Visible = false
                });

            shipHitted = new SoundPlayer(Properties.Resources.explosion);
            splash = new SoundPlayer(Properties.Resources.splash);

            placePlayerFleet(true);
            placePlayerFleet(false);

            mutexPlayerCanFire = false;
        }

        /// <summary>
        ///  Closes BattleFieldForm UI & invokes HighScoreForm UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BattleFieldForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 1)
                Application.OpenForms[0].Show();
        }

        private Point getCoordsFromCell(Location cell)
        {
            int cellDist = p01.Left - p00.Left;

            int cellX = (int)(cell.column * cellDist + p00.Location.X);
            int cellY = (int)((cell.row * cellDist + p00.Location.Y) + (float)(cell.row * cellDist / 13.75));

            return new Point(cellX, cellY);
        }

        private void placePlayerFleet(bool isPlayer1)
        {
            List<IDrawShip> fleet = gameManager.GameState.GetPlayerFleet(isPlayer1);
            List<PictureBox> fleetPboxes = isPlayer1 ? player1FleetPboxes : player2FleetPboxes;
            KryptonGroupBox selectedGbox = isPlayer1 ? Player1GridGbox : Player2GridGbox;

            foreach (var ship in fleet)
            {
                Point coords = getCoordsFromCell(ship.InitCell);
                PictureBox shipPbox = fleetPboxes.Single(pbox => (string)pbox.Tag == ship.Name);
                shipPbox.Location = new Point(selectedGbox.Location.X + coords.X + 6,
                                              selectedGbox.Location.Y + coords.Y + p00.Height + 2);
                Controls.Add(shipPbox);
                shipPbox.BringToFront();

                if (!isPlayer1)
                {
                    bool shipPboxIsVertical = shipPbox.Width < shipPbox.Height;
                    if (shipPboxIsVertical != ship.IsVertical)
                    {
                        Image img = shipPbox.Image;
                        shipPbox.Size = new Size(shipPbox.Size.Height, shipPbox.Size.Width);
                        img.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        shipPbox.Image = img;
                    }
                }
            }
        }

        private Location getCellFromCoords(Point p)
        {
            int cellDist = p01.Left - p00.Left;

            int cellIndexColumn = (int)Math.Round((float)((p.X - p00.Location.X) / cellDist));
            int cellIndexRow = (int)Math.Round((float)((p.Y - p00.Location.Y) / cellDist));

            return new Location((uint)cellIndexRow, (uint)cellIndexColumn);
        }

        private async void computerFire()
        {
            mutexPlayerCanFire = true;
            await Task.Delay(computerShotDelay);

            Location firedAt;
            bool isPlayer1Turn = gameManager.GameState.IsPlayer1Turn;

            while (true)
            {
                int randRow = random.Next(0, player1Grid.GetLength(0));
                int randColumn = random.Next(0, player1Grid.GetLength(1));
                firedAt = new Location((uint)randRow, (uint)randColumn);

                try
                {
                    gameManager.GameState.PlayerFires(firedAt, isPlayer1Turn);
                    break;
                }
                catch (InvalidShotException) { }
            }

            Panel targetCell = player1Grid[firedAt.row, firedAt.column];
            if (gameManager.GameState.PlayerCellContainsShip(firedAt, !isPlayer1Turn))
                hitShip(isPlayer1Turn, firedAt, targetCell);
            else
            {
                targetCell.BackColor = Color.CornflowerBlue;
                splash.Play();
            }

            if (gameManager.GameState.IsGameOver())
            {
                gameOverActions();
                return;
            }

            gameManager.GameState.ChangePlayerTurn();
            mutexPlayerCanFire = false;
        }

        private void showGameOverMessage()
        {
            GameState gs = gameManager.GameState;

            bool player1Won = gs.IsPlayer1Winner();
            string player1Name = gs.GetPlayerName(true);
            string player2Name = gs.GetPlayerName(false);
            string name = player1Won ? player1Name : player2Name;
            MessageBox.Show($"{name} is the winner!", "GameOver");
            new CommanderStatsForm(gameManager).Show();
            Close();
        }

        private void gameOverActions()
		{
            gameManager.GameState.UpdateScore();
            showGameOverMessage();
		}

        private void enemyGridClick(object sender, System.EventArgs e)
        {
            playerMessageLabel.Text = "";

            if (mutexPlayerCanFire) return;

            if (gameManager.GameState.IsGameOver())
                return;

            Panel targetCell = (Panel)sender;
            Location firedAt = getCellFromCoords(targetCell.Location);
            bool isPlayer1Turn = gameManager.GameState.IsPlayer1Turn;

            try
            {
                gameManager.GameState.PlayerFires(firedAt, isPlayer1Turn);
            }
            catch (InvalidShotException) { return; }

            if (gameManager.GameState.PlayerCellContainsShip(firedAt, !isPlayer1Turn))
                hitShip(isPlayer1Turn, firedAt, targetCell);
            else
            {
                targetCell.BackColor = Color.CornflowerBlue;
                splash.Play();
            }

            gameManager.GameState.ChangePlayerTurn();

            if (gameManager.GameState.IsGameOver())
            {
                gameOverActions();
                return;
            }

            computerFire();
        }

        private void hitShip(bool isPlayer1Turn, Location firedAt, Panel targetCell)
        {
            List<IDrawShip> playerFleet = gameManager.GameState.GetPlayerFleet(!isPlayer1Turn);
            string shipName = "";
            Location shipInitCell = null;
            bool shipIsSunk = false;

            foreach (var ship in playerFleet)
                if (ship.IsOccupiedCell(firedAt))
                {
                    shipName = ship.Name;
                    shipInitCell = ship.InitCell;
                    shipIsSunk = ship.IsSank();
                    break;
                }

            List<PictureBox> playerFleetPboxes = isPlayer1Turn ? player2FleetPboxes : player1FleetPboxes;
            PictureBox shipPbox = playerFleetPboxes.Single(pbox => (string)pbox.Tag == shipName);

            PictureBox firePbox = new PictureBox()
            {
                Width = (int)(0.8 * targetCell.Width),
                Height = (int)(0.8 * targetCell.Height),
                Image = Properties.Resources.Fire,
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            Controls.Add(firePbox); 

            if (isPlayer1Turn)
            {
                firePbox.Parent = targetCell;
                firePbox.Location = new Point(targetCell.Width / 2 - firePbox.Width / 2, targetCell.Height / 2 - firePbox.Height / 2);
                enemyFirePboxes[shipName].Add(firePbox);
            }
            else
            {
                firePbox.Parent = shipPbox;
                if (shipPbox.Width > shipPbox.Height)
                    firePbox.Location = new Point(targetCell.Width / 2 - firePbox.Width / 2 + (targetCell.Width * ((int)firedAt.column - (int)shipInitCell.column)), -2);
                else
                    firePbox.Location = new Point(0, targetCell.Height / 2 - firePbox.Height / 2 - 5 + (targetCell.Height * ((int)firedAt.row - (int)shipInitCell.row)));
            }

            firePbox.BringToFront();
            
            if (shipIsSunk)
            {
                if (isPlayer1Turn)
                {
                    List<PictureBox> shipFirePBoxes = enemyFirePboxes[shipName];
                    for (int i = shipFirePBoxes.Count - 1; i >= 0; i--)
                    {
                        shipFirePBoxes[i].Dispose();
                        shipFirePBoxes.RemoveAt(i);
                    }
                    shipPbox.Visible = true;
                }

                Image originalImg = (Bitmap)shipPbox.Image.Clone();
                shipPbox.Image = ImageUtils.SetBitmapAlpha((Bitmap)originalImg, 150);

                string playerName = gameManager.GameState.GetPlayerName(!isPlayer1Turn);
                string sunkShipMsg = $"My {shipName} has sunk!";
                playerMessageLabel.Text = playerName + ": " + sunkShipMsg;
            }

            shipHitted.Play();
        }
    }
}