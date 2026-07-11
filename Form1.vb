Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.IO

Public Class Form1
    Inherits Form

    Private Enum GameMode
        None
        Solo
        Local2P
        NetworkHost
        NetworkClient
    End Enum

    Private WithEvents TickTimer As New Timer()
    Private net As NetworkPeer
    Private game As New WebSlingerGame()

    Private currentMode As GameMode = GameMode.None
    Private isHost As Boolean = False
    Private isConnected As Boolean = False
    Private localPlayerIndex As Integer = 0
    Private frameCounter As Integer = 0

    ' Phim Player 1 (dung cho Solo, Local2P-P1, va nguoi choi local trong che do mang)
    Private keyLeft As Boolean
    Private keyRight As Boolean
    Private keyUp As Boolean
    Private keyDown As Boolean
    Private keyJump As Boolean
    Private keyShoot As Boolean
    Private keySwing As Boolean

    ' Phim Player 2 - CHI dung trong che do Local2P
    Private keyLeftP1 As Boolean
    Private keyRightP1 As Boolean
    Private keyUpP1 As Boolean
    Private keyDownP1 As Boolean
    Private keyJumpP1 As Boolean
    Private keyShootP1 As Boolean
    Private keySwingP1 As Boolean

    Private spPlayer0 As Bitmap
    Private spPlayer0Walk2 As Bitmap
    Private spPlayer0Jump As Bitmap
    Private spPlayer1 As Bitmap
    Private spPlayer1Walk2 As Bitmap
    Private spPlayer1Jump As Bitmap
    Private spThug As Bitmap
    Private spThugWalk2 As Bitmap
    Private spSniperBase As Bitmap
    Private spSniperBarrel As Bitmap
    Private spBoss As Bitmap
    Private spBossWalk2 As Bitmap
    Private spGround As Bitmap
    Private spRoof As Bitmap
    Private spWeb As Bitmap
    Private spBulletEnemy As Bitmap
    Private spPowerWeb As Bitmap
    Private spPowerLife As Bitmap
    Private spBackground As Bitmap

    Private lblStatus As New Label()
    Private btnSolo As New Button()
    Private btnLocal2P As New Button()
    Private lblOnlineSep As New Label()
    Private btnHost As New Button()
    Private btnJoin As New Button()
    Private txtIp As New TextBox()
    Private pnlMenu As New Panel()

    Public Sub New()
        Me.Text = "Nguoi Giang To - Web-Slinger Co-op"
        Me.ClientSize = New Size(WebSlingerGame.VIEW_WIDTH_PX, WebSlingerGame.VIEW_HEIGHT_PX)
        Me.DoubleBuffered = True
        Me.KeyPreview = True
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen

        LoadSpritesIfExist()
        BuildMenuUI()

        TickTimer.Interval = WebSlingerGame.TICK_MS
    End Sub

    ' ===================== UI CHON CHE DO CHOI =====================
    Private Sub BuildMenuUI()
        pnlMenu.Size = New Size(320, 330)
        pnlMenu.Location = New Point((Me.ClientSize.Width - pnlMenu.Width) \ 2, (Me.ClientSize.Height - pnlMenu.Height) \ 2)
        pnlMenu.BackColor = Color.FromArgb(230, 20, 20, 30)

        lblStatus.Text = "Chon che do choi"
        lblStatus.ForeColor = Color.White
        lblStatus.AutoSize = True
        lblStatus.Location = New Point(20, 4)

        btnSolo.Text = "Choi 1 minh"
        btnSolo.Size = New Size(280, 36)
        btnSolo.Location = New Point(20, 30)
        AddHandler btnSolo.Click, AddressOf OnSoloClick

        btnLocal2P.Text = "Choi 2 nguoi (cung may)"
        btnLocal2P.Size = New Size(280, 36)
        btnLocal2P.Location = New Point(20, 74)
        AddHandler btnLocal2P.Click, AddressOf OnLocal2PClick

        lblOnlineSep.Text = "-- Hoac choi qua mang LAN/Online --"
        lblOnlineSep.ForeColor = Color.LightGray
        lblOnlineSep.AutoSize = True
        lblOnlineSep.Location = New Point(20, 124)

        btnHost.Text = "Tao phong (Host)"
        btnHost.Size = New Size(280, 36)
        btnHost.Location = New Point(20, 148)
        AddHandler btnHost.Click, AddressOf OnHostClick

        txtIp.Text = "127.0.0.1"
        txtIp.Size = New Size(280, 24)
        txtIp.Location = New Point(20, 198)

        btnJoin.Text = "Vao phong (Join)"
        btnJoin.Size = New Size(280, 36)
        btnJoin.Location = New Point(20, 232)
        AddHandler btnJoin.Click, AddressOf OnJoinClick

        pnlMenu.Controls.Add(lblStatus)
        pnlMenu.Controls.Add(btnSolo)
        pnlMenu.Controls.Add(btnLocal2P)
        pnlMenu.Controls.Add(lblOnlineSep)
        pnlMenu.Controls.Add(btnHost)
        pnlMenu.Controls.Add(txtIp)
        pnlMenu.Controls.Add(btnJoin)
        Me.Controls.Add(pnlMenu)
    End Sub

    Private Sub OnSoloClick(sender As Object, e As EventArgs)
        currentMode = GameMode.Solo
        isHost = True
        isConnected = False
        localPlayerIndex = 0
        ResetKeys()
        game.SetSoloMode(True)
        pnlMenu.Visible = False
        TickTimer.Start()
    End Sub

    Private Sub OnLocal2PClick(sender As Object, e As EventArgs)
        currentMode = GameMode.Local2P
        isHost = True
        isConnected = False
        localPlayerIndex = 0
        ResetKeys()
        game.SetSoloMode(False)
        pnlMenu.Visible = False
        TickTimer.Start()
    End Sub

    Private Sub OnHostClick(sender As Object, e As EventArgs)
        currentMode = GameMode.NetworkHost
        isHost = True
        localPlayerIndex = 0
        ResetKeys()
        game.SetSoloMode(False)
        net = New NetworkPeer(Me)
        AddHandler net.LineReceived, AddressOf OnLineReceived
        AddHandler net.Connected, AddressOf OnPeerConnected
        AddHandler net.Disconnected, AddressOf OnPeerDisconnected
        net.StartHost(9899)
        lblStatus.Text = "Dang cho nguoi choi thu 2 ket noi... (port 9899)"
    End Sub

    Private Sub OnJoinClick(sender As Object, e As EventArgs)
        currentMode = GameMode.NetworkClient
        isHost = False
        localPlayerIndex = 1
        ResetKeys()
        game.SetSoloMode(False)
        net = New NetworkPeer(Me)
        AddHandler net.LineReceived, AddressOf OnLineReceived
        AddHandler net.Connected, AddressOf OnPeerConnected
        AddHandler net.Disconnected, AddressOf OnPeerDisconnected
        net.ConnectToHost(txtIp.Text.Trim(), 9899)
        lblStatus.Text = "Dang ket noi den " & txtIp.Text.Trim() & " ..."
    End Sub

    Private Sub OnPeerConnected()
        isConnected = True
        pnlMenu.Visible = False
        TickTimer.Start()
    End Sub

    Private Sub OnPeerDisconnected()
        isConnected = False
        TickTimer.Stop()
        currentMode = GameMode.None
        pnlMenu.Visible = True
        lblStatus.Text = "Mat ket noi. Chon lai che do choi."
    End Sub

    Private Sub ReturnToMenuOffline()
        TickTimer.Stop()
        currentMode = GameMode.None
        ResetKeys()
        game = New WebSlingerGame()
        pnlMenu.Visible = True
        lblStatus.Text = "Chon che do choi"
        Me.Invalidate()
    End Sub

    Private Sub ResetKeys()
        keyLeft = False : keyRight = False : keyUp = False : keyDown = False : keyJump = False : keyShoot = False : keySwing = False
        keyLeftP1 = False : keyRightP1 = False : keyUpP1 = False : keyDownP1 = False : keyJumpP1 = False : keyShootP1 = False : keySwingP1 = False
    End Sub

    ' ===================== NHAN DU LIEU MANG =====================
    Private Sub OnLineReceived(line As String)
        If currentMode = GameMode.NetworkHost Then
            If line.StartsWith("INPUT|") Then
                Dim inp As WebSlingerGame.PlayerInput = WebSlingerGame.ParseInput(line)
                game.SetInput(1, inp)
            End If
        ElseIf currentMode = GameMode.NetworkClient Then
            If line.StartsWith("STATE|") Then
                game.ApplyStateLine(line)
            End If
        End If
    End Sub

    ' ===================== VONG LAP CHINH =====================
    Private Sub TickTimer_Tick(sender As Object, e As EventArgs) Handles TickTimer.Tick
        frameCounter += 1

        Dim inpP1 As New WebSlingerGame.PlayerInput()
        inpP1.Left = keyLeft
        inpP1.Right = keyRight
        inpP1.Up = keyUp
        inpP1.Down = keyDown
        inpP1.Jump = keyJump
        inpP1.Shoot = keyShoot
        inpP1.Swing = keySwing

        Select Case currentMode
            Case GameMode.Solo
                game.SetInput(0, inpP1)
                game.Tick()

            Case GameMode.Local2P
                Dim inpP2 As New WebSlingerGame.PlayerInput()
                inpP2.Left = keyLeftP1
                inpP2.Right = keyRightP1
                inpP2.Up = keyUpP1
                inpP2.Down = keyDownP1
                inpP2.Jump = keyJumpP1
                inpP2.Shoot = keyShootP1
                inpP2.Swing = keySwingP1
                game.SetInput(0, inpP1)
                game.SetInput(1, inpP2)
                game.Tick()

            Case GameMode.NetworkHost
                game.SetInput(0, inpP1)
                game.Tick()
                If isConnected Then
                    net.SendLine(game.SerializeState())
                End If

            Case GameMode.NetworkClient
                If isConnected Then
                    net.SendLine(WebSlingerGame.SerializeInput(inpP1))
                End If
        End Select

        Me.Invalidate()
    End Sub

    ' ===================== BAT PHIM =====================
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Escape AndAlso (currentMode = GameMode.Solo OrElse currentMode = GameMode.Local2P) Then
            ReturnToMenuOffline()
            MyBase.OnKeyDown(e)
            Return
        End If
        SetKeyState(e.KeyCode, True)
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub OnKeyUp(e As KeyEventArgs)
        SetKeyState(e.KeyCode, False)
        MyBase.OnKeyUp(e)
    End Sub

    Private Sub SetKeyState(key As Keys, isDown As Boolean)
        If currentMode = GameMode.Local2P Then
            ' Nguoi choi 1 (trai): W A S D + Space (nhay) + LeftCtrl (ban to) + Q (du day to)
            Select Case key
                Case Keys.A : keyLeft = isDown
                Case Keys.D : keyRight = isDown
                Case Keys.W : keyUp = isDown
                Case Keys.S : keyDown = isDown
                Case Keys.Space : keyJump = isDown
                Case Keys.ControlKey : keyShoot = isDown
                Case Keys.Q : keySwing = isDown
            End Select
            ' Nguoi choi 2 (phai): phim mui ten + Enter (nhay) + / (ban to) + . (du day to)
            Select Case key
                Case Keys.Left : keyLeftP1 = isDown
                Case Keys.Right : keyRightP1 = isDown
                Case Keys.Up : keyUpP1 = isDown
                Case Keys.Down : keyDownP1 = isDown
                Case Keys.Enter : keyJumpP1 = isDown
                Case Keys.OemQuestion : keyShootP1 = isDown
                Case Keys.OemPeriod : keySwingP1 = isDown
            End Select
            Return
        End If

        ' Cac che do khac (Choi 1 minh, Host, Client qua mang): 1 bo dieu khien duy nhat
        Select Case key
            Case Keys.Left, Keys.A : keyLeft = isDown
            Case Keys.Right, Keys.D : keyRight = isDown
            Case Keys.Up, Keys.W : keyUp = isDown
            Case Keys.Down, Keys.S : keyDown = isDown
            Case Keys.Space, Keys.Z : keyJump = isDown
            Case Keys.ControlKey, Keys.X : keyShoot = isDown
            Case Keys.ShiftKey, Keys.C : keySwing = isDown
        End Select
    End Sub

    ' ===================== VE HINH (GDI+ / sprite fallback) =====================
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.InterpolationMode = InterpolationMode.NearestNeighbor
        g.Clear(Color.FromArgb(30, 40, 70))

        DrawBackground(g)
        DrawPlatforms(g)
        DrawPowerUps(g)
        DrawEnemies(g)
        DrawBullets(g)
        DrawSwingLines(g)
        DrawPlayers(g)
        DrawHud(g)

        If (currentMode = GameMode.NetworkHost OrElse currentMode = GameMode.NetworkClient) AndAlso Not isConnected Then
            Using f As New Font("Consolas", 10, FontStyle.Bold)
                g.DrawString("Chua ket noi - dung menu de Host/Join", f, Brushes.White, 10, WebSlingerGame.VIEW_HEIGHT_PX - 24)
            End Using
        ElseIf currentMode = GameMode.Solo OrElse currentMode = GameMode.Local2P Then
            Using f As New Font("Consolas", 9, FontStyle.Bold)
                g.DrawString("ESC de quay ve menu   |   Giu Shift/Q/. tren khong de du day to", f, Brushes.White, 10, WebSlingerGame.VIEW_HEIGHT_PX - 20)
            End Using
        End If

        MyBase.OnPaint(e)
    End Sub

    Private Function WorldToScreenX(worldX As Double) As Integer
        Return CInt(Math.Round(worldX - game.CameraX))
    End Function

    Private Sub DrawBackground(g As Graphics)
        If spBackground IsNot Nothing Then
            Dim offset As Integer = CInt(game.CameraX * 0.3) Mod spBackground.Width
            g.DrawImage(spBackground, -offset, 0)
            g.DrawImage(spBackground, -offset + spBackground.Width, 0)
        Else
            Using skyBrush As New SolidBrush(Color.FromArgb(20, 24, 40))
                g.FillRectangle(skyBrush, 0, 0, WebSlingerGame.VIEW_WIDTH_PX, WebSlingerGame.VIEW_HEIGHT_PX)
            End Using
        End If
    End Sub

    Private Sub DrawPlatforms(g As Graphics)
        For Each plat In game.Platforms
            Dim sx As Integer = WorldToScreenX(plat.X)
            If sx + plat.W < 0 OrElse sx > WebSlingerGame.VIEW_WIDTH_PX Then Continue For

            Dim sprite As Bitmap = If(plat.Kind = WebSlingerGame.PlatformKind.Ground, spGround, spRoof)
            If sprite IsNot Nothing Then
                Dim tileW As Integer = sprite.Width
                Dim tx As Integer = sx
                Do While tx < sx + CInt(plat.W)
                    g.DrawImage(sprite, tx, CInt(plat.Y))
                    tx += tileW
                Loop
            Else
                Dim c As Color = If(plat.Kind = WebSlingerGame.PlatformKind.Ground, Color.FromArgb(70, 70, 75), Color.FromArgb(110, 60, 40))
                Using b As New SolidBrush(c)
                    g.FillRectangle(b, sx, CInt(plat.Y), CInt(plat.W), CInt(plat.H))
                End Using
                g.DrawRectangle(Pens.Black, sx, CInt(plat.Y), CInt(plat.W), CInt(plat.H))
            End If
        Next
    End Sub

    ' Ve day to dang duoc su dung: 1 duong ke tu nguoi choi len diem neo
    Private Sub DrawSwingLines(g As Graphics)
        For i As Integer = 0 To 1
            Dim p As WebSlingerGame.PlayerState = game.Players(i)
            If Not p.Alive OrElse Not p.IsSwinging Then Continue For
            Dim sx As Integer = WorldToScreenX(p.X) + WebSlingerGame.PLAYER_W \ 2
            Dim sy As Integer = CInt(p.Y) + WebSlingerGame.PLAYER_H \ 2
            Dim ax As Integer = WorldToScreenX(p.SwingAnchorX)
            Dim ay As Integer = CInt(p.SwingAnchorY)
            Using pen As New Pen(Color.FromArgb(230, 230, 230), 2)
                g.DrawLine(pen, sx, sy, ax, ay)
            End Using
            Using b As New SolidBrush(Color.White)
                g.FillEllipse(b, ax - 3, ay - 3, 6, 6)
            End Using
        Next
    End Sub

    Private Sub DrawPlayers(g As Graphics)
        For i As Integer = 0 To 1
            Dim p As WebSlingerGame.PlayerState = game.Players(i)
            If Not p.Alive Then Continue For

            Dim sx As Integer = WorldToScreenX(p.X)
            Dim sy As Integer = CInt(p.Y)
            Dim blink As Boolean = (p.InvulnTicks > 0) AndAlso ((p.InvulnTicks \ 4) Mod 2 = 0)
            If blink Then Continue For

            Dim baseSprite As Bitmap = If(i = 0, spPlayer0, spPlayer1)
            Dim walk2Sprite As Bitmap = If(i = 0, spPlayer0Walk2, spPlayer1Walk2)
            Dim jumpSprite As Bitmap = If(i = 0, spPlayer0Jump, spPlayer1Jump)

            Dim sprite As Bitmap
            If (Not p.OnGround OrElse p.IsSwinging) AndAlso jumpSprite IsNot Nothing Then
                sprite = jumpSprite
            ElseIf p.IsMoving AndAlso p.OnGround AndAlso walk2Sprite IsNot Nothing AndAlso ((frameCounter \ 6) Mod 2 = 1) Then
                sprite = walk2Sprite
            Else
                sprite = baseSprite
            End If

            If sprite IsNot Nothing Then
                Dim st As GraphicsState = g.Save()
                If Not p.FacingRight Then
                    g.TranslateTransform(sx + WebSlingerGame.PLAYER_W, sy)
                    g.ScaleTransform(-1, 1)
                    g.DrawImage(sprite, 0, 0, WebSlingerGame.PLAYER_W, WebSlingerGame.PLAYER_H)
                Else
                    g.DrawImage(sprite, sx, sy, WebSlingerGame.PLAYER_W, WebSlingerGame.PLAYER_H)
                End If
                g.Restore(st)
            Else
                Dim c As Color = If(i = 0, Color.Red, Color.DeepSkyBlue)
                Using b As New SolidBrush(c)
                    g.FillRectangle(b, sx, sy, WebSlingerGame.PLAYER_W, WebSlingerGame.PLAYER_H)
                End Using
                g.DrawRectangle(Pens.Black, sx, sy, WebSlingerGame.PLAYER_W, WebSlingerGame.PLAYER_H)
                Dim cx As Integer = sx + WebSlingerGame.PLAYER_W \ 2
                Dim cy As Integer = sy + WebSlingerGame.PLAYER_H \ 2
                g.DrawLine(Pens.White, cx, cy, cx + p.AimDx * 16, cy + p.AimDy * 16)
            End If
        Next
    End Sub

    Private Sub DrawEnemies(g As Graphics)
        For Each en In game.Enemies
            If Not en.Alive Then Continue For
            Dim sx As Integer = WorldToScreenX(en.X)
            If sx < -60 OrElse sx > WebSlingerGame.VIEW_WIDTH_PX + 60 Then Continue For
            Dim sy As Integer = CInt(en.Y)

            Dim sprite As Bitmap = Nothing
            Dim fallbackColor As Color = Color.DarkOrange
            Dim w As Integer = WebSlingerGame.THUG_W
            Dim h As Integer = WebSlingerGame.THUG_H
            Dim useWalk2 As Boolean = ((frameCounter \ 6) Mod 2 = 1)

            Select Case en.Kind
                Case WebSlingerGame.EnemyType.Thug
                    sprite = If(useWalk2 AndAlso spThugWalk2 IsNot Nothing, spThugWalk2, spThug)
                    fallbackColor = Color.SaddleBrown
                Case WebSlingerGame.EnemyType.Sniper
                    fallbackColor = Color.DarkSlateGray
                    w = WebSlingerGame.SNIPER_W : h = WebSlingerGame.SNIPER_H
                Case WebSlingerGame.EnemyType.Boss
                    sprite = If(useWalk2 AndAlso spBossWalk2 IsNot Nothing, spBossWalk2, spBoss)
                    fallbackColor = Color.DarkRed : w = WebSlingerGame.BOSS_W : h = WebSlingerGame.BOSS_H
            End Select

            If en.Kind = WebSlingerGame.EnemyType.Sniper AndAlso spSniperBase IsNot Nothing AndAlso spSniperBarrel IsNot Nothing Then
                DrawSniper(g, en, sx, sy, w, h)
            ElseIf sprite IsNot Nothing Then
                If Not en.FacingRight Then
                    Dim st As GraphicsState = g.Save()
                    g.TranslateTransform(sx + w, sy)
                    g.ScaleTransform(-1, 1)
                    g.DrawImage(sprite, 0, 0, w, h)
                    g.Restore(st)
                Else
                    g.DrawImage(sprite, sx, sy, w, h)
                End If
            Else
                Using b As New SolidBrush(fallbackColor)
                    g.FillRectangle(b, sx, sy, w, h)
                End Using
                g.DrawRectangle(Pens.Black, sx, sy, w, h)
            End If

            If en.Kind = WebSlingerGame.EnemyType.Boss Then
                Dim barW As Integer = WebSlingerGame.BOSS_W
                g.DrawRectangle(Pens.White, sx, sy - 12, barW, 7)
                Dim hpRatio As Double = Math.Max(0.0, Math.Min(1.0, en.HP / 14.0))
                Using hb As New SolidBrush(Color.Red)
                    g.FillRectangle(hb, sx, sy - 12, CInt(barW * hpRatio), 7)
                End Using
            End If
        Next
    End Sub

    Private Sub DrawSniper(g As Graphics, en As WebSlingerGame.EnemyState, sx As Integer, sy As Integer, w As Integer, h As Integer)
        g.DrawImage(spSniperBase, sx, sy, w, h)
        Dim pivotX As Integer = sx + w \ 2
        Dim pivotY As Integer = sy + h \ 2
        Dim barrelW As Integer = spSniperBarrel.Width
        Dim barrelH As Integer = spSniperBarrel.Height

        Dim st As GraphicsState = g.Save()
        g.TranslateTransform(pivotX, pivotY)
        g.RotateTransform(CSng(en.AimAngleDeg))
        g.DrawImage(spSniperBarrel, 0, -(barrelH \ 2), barrelW, barrelH)
        g.Restore(st)
    End Sub

    Private Sub DrawBullets(g As Graphics)
        For Each b In game.Bullets
            If Not b.Active Then Continue For
            Dim sx As Integer = WorldToScreenX(b.X)
            If sx < -20 OrElse sx > WebSlingerGame.VIEW_WIDTH_PX + 20 Then Continue For
            Dim sy As Integer = CInt(b.Y)

            Dim sprite As Bitmap = If(b.Owner >= 0, spWeb, spBulletEnemy)
            If sprite IsNot Nothing Then
                g.DrawImage(sprite, sx - 5, sy - 3, 10, 6)
            Else
                Dim c As Color = If(b.Owner >= 0, Color.WhiteSmoke, Color.Magenta)
                Using pen As New Pen(c, 2)
                    g.DrawLine(pen, sx, sy, CInt(sx - b.DirX), CInt(sy - b.DirY))
                End Using
            End If
        Next
    End Sub

    Private Sub DrawPowerUps(g As Graphics)
        For Each pu In game.PowerUps
            If Not pu.Active Then Continue For
            Dim sx As Integer = WorldToScreenX(pu.X)
            If sx < -40 OrElse sx > WebSlingerGame.VIEW_WIDTH_PX + 40 Then Continue For
            Dim sy As Integer = CInt(pu.Y)

            Dim sprite As Bitmap = If(pu.Kind = WebSlingerGame.PowerUpType.WebUp, spPowerWeb, spPowerLife)
            If sprite IsNot Nothing Then
                g.DrawImage(sprite, sx, sy, 28, 28)
            Else
                Dim c As Color = If(pu.Kind = WebSlingerGame.PowerUpType.WebUp, Color.WhiteSmoke, Color.LimeGreen)
                Using b As New SolidBrush(c)
                    g.FillEllipse(b, sx, sy, 28, 28)
                End Using
                g.DrawEllipse(Pens.Black, sx, sy, 28, 28)
            End If
        Next
    End Sub

    Private Sub DrawHud(g As Graphics)
        Using f As New Font("Consolas", 11, FontStyle.Bold)
            Dim txt As String
            If game.SoloMode Then
                txt = String.Format("Mang: {0}   To Lv{1}", game.SharedLives, game.Players(0).WebLevel)
            Else
                txt = String.Format("Mang: {0}   P1 To Lv{1}   P2 To Lv{2}",
                    game.SharedLives, game.Players(0).WebLevel, game.Players(1).WebLevel)
            End If
            g.DrawString(txt, f, Brushes.White, 8, 6)

            If game.GameOver Then
                DrawCenteredBanner(g, "GAME OVER", Color.Red)
            ElseIf game.Victory Then
                DrawCenteredBanner(g, "CHIEN THANG!", Color.Gold)
            End If
        End Using
    End Sub

    Private Sub DrawCenteredBanner(g As Graphics, text As String, c As Color)
        Using f As New Font("Consolas", 28, FontStyle.Bold)
            Dim sz As SizeF = g.MeasureString(text, f)
            Dim x As Single = (WebSlingerGame.VIEW_WIDTH_PX - sz.Width) / 2.0F
            Dim y As Single = (WebSlingerGame.VIEW_HEIGHT_PX - sz.Height) / 2.0F
            Using b As New SolidBrush(c)
                g.DrawString(text, f, b, x, y)
            End Using
        End Using
    End Sub

    ' ===================== NAP SPRITE (tuy chon) =====================
    Private Sub LoadSpritesIfExist()
        Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
        Dim assetsDir As String = Path.Combine(dir, "Assets")

        spPlayer0 = TryLoad(assetsDir, "player0.png")
        spPlayer0Walk2 = TryLoad(assetsDir, "player0_walk2.png")
        spPlayer0Jump = TryLoad(assetsDir, "player0_jump.png")
        spPlayer1 = TryLoad(assetsDir, "player1.png")
        spPlayer1Walk2 = TryLoad(assetsDir, "player1_walk2.png")
        spPlayer1Jump = TryLoad(assetsDir, "player1_jump.png")
        spThug = TryLoad(assetsDir, "enemy_thug.png")
        spThugWalk2 = TryLoad(assetsDir, "enemy_thug_walk2.png")
        spSniperBase = TryLoad(assetsDir, "enemy_sniper_base.png")
        spSniperBarrel = TryLoad(assetsDir, "enemy_sniper_barrel.png")
        spBoss = TryLoad(assetsDir, "enemy_boss.png")
        spBossWalk2 = TryLoad(assetsDir, "enemy_boss_walk2.png")
        spGround = TryLoad(assetsDir, "tile_ground.png")
        spRoof = TryLoad(assetsDir, "tile_roof.png")
        spWeb = TryLoad(assetsDir, "web_shot.png")
        spBulletEnemy = TryLoad(assetsDir, "bullet_enemy.png")
        spPowerWeb = TryLoad(assetsDir, "powerup_web.png")
        spPowerLife = TryLoad(assetsDir, "powerup_life.png")
        spBackground = TryLoad(assetsDir, "background.png")
    End Sub

    Private Function TryLoad(assetsDir As String, fileName As String) As Bitmap
        Try
            Dim fullPath As String = Path.Combine(assetsDir, fileName)
            If File.Exists(fullPath) Then
                Return New Bitmap(fullPath)
            End If
        Catch ex As Exception
        End Try
        Return Nothing
    End Function

End Class
