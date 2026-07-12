Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.IO
Imports System.Linq

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

    ' Theo doi thoi diem vua cham dat (de hien khung "_land" trong vai tick) va
    ' trang thai OnGround truoc do cua tung nguoi choi, dung phat hien thoi diem tiep dat.
    Private landTimerP(1) As Integer
    Private prevOnGroundP(1) As Boolean

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

    ' Sprite nhan vat: mang 2 chieu [skinIndex 0..3, poseIndex 0..7].
    ' poseIndex: 0=idle 1=walk2 2=jump 3=swing 4=flip 5=land 6=wallcrouch 7=shootair
    Private Const POSE_IDLE As Integer = 0
    Private Const POSE_WALK2 As Integer = 1
    Private Const POSE_JUMP As Integer = 2
    Private Const POSE_SWING As Integer = 3
    Private Const POSE_FLIP As Integer = 4
    Private Const POSE_LAND As Integer = 5
    Private Const POSE_WALLCROUCH As Integer = 6
    Private Const POSE_SHOOTAIR As Integer = 7

    Private ReadOnly skinPrefixes() As String = {"player0", "player1", "player2", "player3"}
    Private ReadOnly poseSuffixes() As String = {"", "_walk2", "_jump", "_swing", "_flip", "_land", "_wallcrouch", "_shootair"}
    Private ReadOnly skinNames() As String = {"Do - Den", "Xanh duong - Bac", "Luc - Vang", "Tim - Cam"}
    Private ReadOnly skinFallbackColors() As Color = {Color.Red, Color.DeepSkyBlue, Color.LimeGreen, Color.Orange}
    Private skinSprites(3, 7) As Bitmap

    Private spThug As Bitmap
    Private spThugWalk2 As Bitmap
    Private spSniperBase As Bitmap
    Private spSniperBarrel As Bitmap
    Private spBoss As Bitmap
    Private spBossWalk2 As Bitmap
    Private spGround As Bitmap
    Private spRoof As Bitmap
    Private spPit As Bitmap
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
    Private pnlSkin As New Panel()

    ' Skin nguoi choi cuc bo hien tai da chon (dung de gui qua mang khi ket noi thanh cong)
    Private myLocalSkin As Integer = 0

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

    ' Hien bang chon skin (4 lua chon) roi goi onChosen(skinIndex) khi nguoi choi bam vao 1 nut.
    ' Dung chung cho ca 4 luong: Solo, Local2P (goi 2 lan lien tiep cho P1 roi P2), Host, Join.
    Private Sub ShowSkinPicker(title As String, onChosen As Action(Of Integer))
        pnlMenu.Visible = False

        pnlSkin.Controls.Clear()
        pnlSkin.Size = New Size(320, 30 + WebSlingerGame.SKIN_COUNT * 44 + 10)
        pnlSkin.Location = New Point((Me.ClientSize.Width - pnlSkin.Width) \ 2, (Me.ClientSize.Height - pnlSkin.Height) \ 2)
        pnlSkin.BackColor = Color.FromArgb(230, 20, 20, 30)

        Dim lblTitle As New Label()
        lblTitle.Text = title
        lblTitle.ForeColor = Color.White
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(20, 4)
        pnlSkin.Controls.Add(lblTitle)

        For i As Integer = 0 To WebSlingerGame.SKIN_COUNT - 1
            Dim btn As New Button()
            btn.Text = skinNames(i)
            btn.Size = New Size(280, 36)
            btn.Location = New Point(20, 30 + i * 44)
            Dim capturedIndex As Integer = i
            AddHandler btn.Click, Sub(s2 As Object, e2 As EventArgs)
                                       pnlSkin.Visible = False
                                       onChosen(capturedIndex)
                                   End Sub
            pnlSkin.Controls.Add(btn)
        Next

        If Not Me.Controls.Contains(pnlSkin) Then Me.Controls.Add(pnlSkin)
        pnlSkin.Visible = True
        pnlSkin.BringToFront()
    End Sub

    Private Sub OnSoloClick(sender As Object, e As EventArgs)
        ShowSkinPicker("Chon skin nhan vat", Sub(skin As Integer)
                                                  currentMode = GameMode.Solo
                                                  isHost = True
                                                  isConnected = False
                                                  localPlayerIndex = 0
                                                  ResetKeys()
                                                  game.SetSoloMode(True)
                                                  game.SetSkin(0, skin)
                                                  pnlMenu.Visible = False
                                                  TickTimer.Start()
                                              End Sub)
    End Sub

    Private Sub OnLocal2PClick(sender As Object, e As EventArgs)
        ShowSkinPicker("Nguoi choi 1: chon skin", Sub(skinP0 As Integer)
                                                       ShowSkinPicker("Nguoi choi 2: chon skin", Sub(skinP1 As Integer)
                                                                                                     currentMode = GameMode.Local2P
                                                                                                     isHost = True
                                                                                                     isConnected = False
                                                                                                     localPlayerIndex = 0
                                                                                                     ResetKeys()
                                                                                                     game.SetSoloMode(False)
                                                                                                     game.SetSkin(0, skinP0)
                                                                                                     game.SetSkin(1, skinP1)
                                                                                                     pnlMenu.Visible = False
                                                                                                     TickTimer.Start()
                                                                                                 End Sub)
                                                   End Sub)
    End Sub

    Private Sub OnHostClick(sender As Object, e As EventArgs)
        ShowSkinPicker("Chon skin nhan vat", Sub(skin As Integer)
                                                  myLocalSkin = skin
                                                  currentMode = GameMode.NetworkHost
                                                  isHost = True
                                                  localPlayerIndex = 0
                                                  ResetKeys()
                                                  game.SetSoloMode(False)
                                                  game.SetSkin(0, skin)
                                                  net = New NetworkPeer(Me)
                                                  AddHandler net.LineReceived, AddressOf OnLineReceived
                                                  AddHandler net.Connected, AddressOf OnPeerConnected
                                                  AddHandler net.Disconnected, AddressOf OnPeerDisconnected
                                                  net.StartHost(9899)
                                                  pnlMenu.Visible = True
                                                  lblStatus.Text = "Dang cho nguoi choi thu 2 ket noi... (port 9899)"
                                              End Sub)
    End Sub

    Private Sub OnJoinClick(sender As Object, e As EventArgs)
        ShowSkinPicker("Chon skin nhan vat", Sub(skin As Integer)
                                                  myLocalSkin = skin
                                                  currentMode = GameMode.NetworkClient
                                                  isHost = False
                                                  localPlayerIndex = 1
                                                  ResetKeys()
                                                  game.SetSoloMode(False)
                                                  game.SetSkin(1, skin)
                                                  net = New NetworkPeer(Me)
                                                  AddHandler net.LineReceived, AddressOf OnLineReceived
                                                  AddHandler net.Connected, AddressOf OnPeerConnected
                                                  AddHandler net.Disconnected, AddressOf OnPeerDisconnected
                                                  net.ConnectToHost(txtIp.Text.Trim(), 9899)
                                                  pnlMenu.Visible = True
                                                  lblStatus.Text = "Dang ket noi den " & txtIp.Text.Trim() & " ..."
                                              End Sub)
    End Sub

    Private Sub OnPeerConnected()
        isConnected = True
        pnlMenu.Visible = False
        net.SendLine("SKIN|" & localPlayerIndex.ToString() & "|" & myLocalSkin.ToString())
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
        If line.StartsWith("SKIN|") Then
            Dim parts As String() = line.Split("|"c)
            If parts.Length >= 3 Then
                Dim idx As Integer
                Dim skin As Integer
                If Integer.TryParse(parts(1), idx) AndAlso Integer.TryParse(parts(2), skin) Then
                    game.SetSkin(idx, skin)
                End If
            End If
            Return
        End If

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

        ' Phat hien thoi diem vua tiep dat (OnGround chuyen tu False -> True) de
        ' kich hoat hien khung "_land" trong vai tick, khong phu thuoc che do choi.
        For i As Integer = 0 To 1
            Dim p As WebSlingerGame.PlayerState = game.Players(i)
            If p.OnGround AndAlso Not prevOnGroundP(i) Then
                landTimerP(i) = 8
            ElseIf landTimerP(i) > 0 Then
                landTimerP(i) -= 1
            End If
            prevOnGroundP(i) = p.OnGround
        Next

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
        DrawPits(g)
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

    ' Tim khoang trong giua cac doan duong (Ground) lien tiep va ve tile vuc lap day,
    ' keo dai tu mat duong xuong het day man hinh. Chi la hieu ung hinh anh - viec
    ' roi xuong vuc da bi tinh la "mat mang" san trong ResolvePlatformCollision.
    Private Sub DrawPits(g As Graphics)
        Dim grounds = game.Platforms.
            Where(Function(p) p.Kind = WebSlingerGame.PlatformKind.Ground).
            OrderBy(Function(p) p.X).ToList()

        For i As Integer = 0 To grounds.Count - 2
            Dim gapStart As Double = grounds(i).X + grounds(i).W
            Dim gapEnd As Double = grounds(i + 1).X
            If gapEnd > gapStart Then
                DrawPitSegment(g, gapStart, gapEnd)
            End If
        Next
    End Sub

    Private Sub DrawPitSegment(g As Graphics, worldStart As Double, worldEnd As Double)
        Dim sx As Integer = WorldToScreenX(worldStart)
        Dim ex As Integer = WorldToScreenX(worldEnd)
        If ex < 0 OrElse sx > WebSlingerGame.VIEW_WIDTH_PX Then Return

        Dim topY As Integer = WebSlingerGame.GROUND_Y
        Dim bottomY As Integer = WebSlingerGame.VIEW_HEIGHT_PX

        If spPit IsNot Nothing Then
            Dim tileW As Integer = spPit.Width
            Dim tileH As Integer = spPit.Height
            Dim tx As Integer = sx
            Do While tx < ex
                Dim ty As Integer = topY
                Do While ty < bottomY
                    g.DrawImage(spPit, tx, ty, tileW, tileH)
                    ty += tileH
                Loop
                tx += tileW
            Loop
        Else
            Using b As New SolidBrush(Color.Black)
                g.FillRectangle(b, sx, topY, ex - sx, bottomY - topY)
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

            Dim skin As Integer = Math.Max(0, Math.Min(3, p.SkinIndex))
            Dim baseSprite As Bitmap = skinSprites(skin, POSE_IDLE)
            Dim walk2Sprite As Bitmap = skinSprites(skin, POSE_WALK2)
            Dim jumpSprite As Bitmap = skinSprites(skin, POSE_JUMP)
            Dim swingSprite As Bitmap = skinSprites(skin, POSE_SWING)
            Dim flipSprite As Bitmap = skinSprites(skin, POSE_FLIP)
            Dim landSprite As Bitmap = skinSprites(skin, POSE_LAND)
            Dim wallcrouchSprite As Bitmap = skinSprites(skin, POSE_WALLCROUCH)
            Dim shootAirSprite As Bitmap = skinSprites(skin, POSE_SHOOTAIR)

            ' "Vua ban" = ShootCooldown vua duoc dat lai ve gia tri toi da cua cap do hien tai
            Dim justFired As Boolean = (p.ShootCooldown = game.CooldownForLevel(p.WebLevel)) AndAlso p.ShootCooldown > 0

            Dim sprite As Bitmap
            If p.IsSwinging AndAlso swingSprite IsNot Nothing Then
                sprite = swingSprite
            ElseIf Not p.OnGround AndAlso justFired AndAlso shootAirSprite IsNot Nothing Then
                sprite = shootAirSprite
            ElseIf Not p.OnGround AndAlso p.VelY < 0 AndAlso jumpSprite IsNot Nothing Then
                sprite = jumpSprite
            ElseIf Not p.OnGround AndAlso p.VelY >= 0 AndAlso flipSprite IsNot Nothing Then
                sprite = flipSprite
            ElseIf Not p.OnGround AndAlso jumpSprite IsNot Nothing Then
                sprite = jumpSprite
            ElseIf p.OnGround AndAlso landTimerP(i) > 0 AndAlso landSprite IsNot Nothing Then
                sprite = landSprite
            ElseIf p.OnGround AndAlso p.AimDy = 1 AndAlso Not p.IsMoving AndAlso wallcrouchSprite IsNot Nothing Then
                sprite = wallcrouchSprite
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
                Dim c As Color = skinFallbackColors(skin)
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

        For s As Integer = 0 To 3
            For pIdx As Integer = 0 To 7
                skinSprites(s, pIdx) = TryLoad(assetsDir, skinPrefixes(s) & poseSuffixes(pIdx) & ".png")
            Next
        Next
        spThug = TryLoad(assetsDir, "enemy_thug.png")
        spThugWalk2 = TryLoad(assetsDir, "enemy_thug_walk2.png")
        spSniperBase = TryLoad(assetsDir, "enemy_sniper_base.png")
        spSniperBarrel = TryLoad(assetsDir, "enemy_sniper_barrel.png")
        spBoss = TryLoad(assetsDir, "enemy_boss.png")
        spBossWalk2 = TryLoad(assetsDir, "enemy_boss_walk2.png")
        spGround = TryLoad(assetsDir, "tile_ground.png")
        spRoof = TryLoad(assetsDir, "tile_roof.png")
        spPit = TryLoad(assetsDir, "tile_pit.png")
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
