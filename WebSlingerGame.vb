Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

''' <summary>
''' Logic game "Nguoi Giang To" (Web-Slinger Co-op, thiet ke goc - khong dung ten/hinh anh
''' nhan vat thuong hieu cua ben thu ba). Kien truc netcode/vong lap tick giu nguyen tinh
''' than ContraGame.vb / PlatformGame.vb (host authoritative, pipe-delimited protocol).
''' Diem khac biet chinh: co che DU DAY TO (pendulum swing) giua cac diem neo tren noc nha,
''' thay cho/bo sung cho nhay thuong.
''' </summary>
Public Class WebSlingerGame

    ' ===================== HANG SO THE GIOI =====================
    Public Const TICK_MS As Integer = 33
    Public Const LEVEL_WIDTH_PX As Integer = 7680
    Public Const VIEW_WIDTH_PX As Integer = 960
    Public Const VIEW_HEIGHT_PX As Integer = 540
    Public Const GROUND_Y As Integer = 472              ' via he / mat duong

    Public Const GRAVITY As Double = 1.02
    Public Const MAX_FALL_SPEED As Double = 16.8
    Public Const MOVE_SPEED As Double = 5.4
    Public Const JUMP_VELOCITY As Double = -16.2
    Public Const AIR_DAMPING As Double = 0.965          ' giam da khi con luot trong khong sau khi nha day
    Public Const PLAYER_W As Integer = 34
    Public Const PLAYER_H As Integer = 46

    ' --- Co che du day to ---
    Public Const SWING_MIN_LEN As Double = 90
    Public Const SWING_MAX_LEN As Double = 280
    Public Const SWING_PUMP_ACCEL As Double = 0.0035    ' nguoi choi "day nguoi" de tang bien do khi du
    Public Const SWING_DAMPING As Double = 0.999
    Public Const SWING_RELEASE_BOOST As Double = 6.0    ' nhay trong luc du -> bat len them mot chut

    Public Const THUG_W As Integer = 40
    Public Const THUG_H As Integer = 46
    Public Const SNIPER_W As Integer = 40
    Public Const SNIPER_H As Integer = 46
    Public Const BOSS_W As Integer = 90
    Public Const BOSS_H As Integer = 90

    Public Const WEB_SPEED As Double = 14.4
    Public Const RAPID_COOLDOWN As Integer = 4          ' cap 1: ban nhanh
    Public Const NORMAL_COOLDOWN As Integer = 9         ' cap 0: ban thuong
    Public Const SPREAD_COOLDOWN As Integer = 12        ' cap 2: ban toe 3 tia

    Public Const RESPAWN_INVULN_TICKS As Integer = 90
    Public Const SHARED_LIVES_START As Integer = 5
    Public Const SKIN_COUNT As Integer = 4

    Public Const ENEMY_MAX_ALIVE As Integer = 5
    Public Const ENEMY_SPAWN_CHECK_TICKS As Integer = 10

    ' ===================== ENUM =====================
    Public Enum EnemyType As Byte
        Thug = 0        ' chay bo duoi duong, ban ngang
        Sniper = 1      ' dung yen tren noc nha, xoay ngam theo nguoi choi
        Boss = 2        ' trum bang dang cuoi man
    End Enum

    Public Enum PowerUpType As Byte
        WebUp = 0       ' binh dung dich to -> nang cap sung to (0->1->2)
        LifeUp = 1      ' +1 mang chung
    End Enum

    Public Enum PlatformKind As Byte
        Ground = 0
        OneWay = 1      ' noc nha / gia bay: chi chan tu tren roi xuong
    End Enum

    ' ===================== STRUCT =====================
    Public Structure PlayerState
        Public X As Double
        Public Y As Double
        Public VelX As Double          ' da ngang con lai sau khi nha day to (tat dan trong khong khi)
        Public VelY As Double
        Public FacingRight As Boolean
        Public AimDx As Integer
        Public AimDy As Integer
        Public OnGround As Boolean
        Public Alive As Boolean
        Public IsMoving As Boolean
        Public WebLevel As Integer         ' 0 = thuong, 1 = ban nhanh, 2 = ban toe 3 tia
        Public InvulnTicks As Integer
        Public ShootCooldown As Integer
        Public RespawnTimer As Integer
        ' --- trang thai du day to ---
        Public IsSwinging As Boolean
        Public SwingAnchorX As Double
        Public SwingAnchorY As Double
        Public SwingLength As Double
        Public SwingAngle As Double         ' radian, 0 = treo thang xuong duoi diem neo
        Public SwingAngularVel As Double
        Public SkinIndex As Integer     ' 0-3, chon nhan vat/skin nao de ve (doc lap voi index P1/P2)
    End Structure

    Public Structure BulletState
        Public X As Double
        Public Y As Double
        Public DirX As Double
        Public DirY As Double
        Public Owner As Integer
        Public Active As Boolean
    End Structure

    Public Structure EnemyState
        Public X As Double
        Public Y As Double
        Public VelX As Double
        Public Kind As EnemyType
        Public HP As Integer
        Public Alive As Boolean
        Public FacingRight As Boolean
        Public AimAngleDeg As Double
        Public ShootCooldown As Integer
        Public PatrolMinX As Double
        Public PatrolMaxX As Double
    End Structure

    Public Structure PowerUpState
        Public X As Double
        Public Y As Double
        Public Kind As PowerUpType
        Public Active As Boolean
        Public TtlTicks As Integer
    End Structure

    Public Structure PlatformRect
        Public X As Double
        Public Y As Double
        Public W As Double
        Public H As Double
        Public Kind As PlatformKind
    End Structure

    ' Diem neo tren noc nha de bam day to
    Public Structure SwingAnchor
        Public X As Double
        Public Y As Double
    End Structure

    Private Structure EnemySpawnDef
        Public SpawnAtCamX As Double
        Public X As Double
        Public Y As Double
        Public Kind As EnemyType
        Public HP As Integer
        Public PatrolMinX As Double
        Public PatrolMaxX As Double
        Public Used As Boolean
    End Structure

    Public Structure PlayerInput
        Public Left As Boolean
        Public Right As Boolean
        Public Up As Boolean
        Public Down As Boolean
        Public Jump As Boolean
        Public Shoot As Boolean
        Public Swing As Boolean     ' giu phim nay tren khong de bam day to vao diem neo gan nhat
    End Structure

    ' ===================== STATE CHINH =====================
    Public Players(1) As PlayerState
    Public Bullets As New List(Of BulletState)
    Public Enemies As New List(Of EnemyState)
    Public PowerUps As New List(Of PowerUpState)
    Public Platforms As New List(Of PlatformRect)
    Public Anchors As New List(Of SwingAnchor)

    Public CameraX As Double = 0
    Public SharedLives As Integer = SHARED_LIVES_START
    Public GameOver As Boolean = False
    Public Victory As Boolean = False
    Public SoloMode As Boolean = False

    Private inputs(1) As PlayerInput
    Private spawnDefs As New List(Of EnemySpawnDef)
    Private tickCount As Integer = 0

    ' ===================== KHOI TAO =====================
    Public Sub New()
        BuildLevel1()
        Players(0) = MakeFreshPlayer(120, GROUND_Y - PLAYER_H)
        Players(1) = MakeFreshPlayer(60, GROUND_Y - PLAYER_H)
        Dim p0 As PlayerState = Players(0) : p0.SkinIndex = 0 : Players(0) = p0
        Dim p1 As PlayerState = Players(1) : p1.SkinIndex = 1 : Players(1) = p1
    End Sub

    ' Chon skin/nhan vat cho mot slot nguoi choi (0 hoac 1). Duoc goi tu menu chon
    ' skin truoc khi vao tran, va cung duoc dong bo qua mang bang message "SKIN|idx|skin".
    Public Sub SetSkin(idx As Integer, skinIndex As Integer)
        If idx < 0 OrElse idx > 1 Then Return
        Dim clamped As Integer = Math.Max(0, Math.Min(SKIN_COUNT - 1, skinIndex))
        Dim p As PlayerState = Players(idx)
        p.SkinIndex = clamped
        Players(idx) = p
    End Sub

    Public Sub SetSoloMode(solo As Boolean)
        SoloMode = solo
        If solo Then
            Dim p1 As PlayerState = Players(1)
            p1.Alive = False
            p1.RespawnTimer = 0
            Players(1) = p1
        End If
    End Sub

    Private Function MakeFreshPlayer(x As Double, y As Double) As PlayerState
        Dim p As New PlayerState()
        p.X = x
        p.Y = y
        p.VelX = 0
        p.VelY = 0
        p.FacingRight = True
        p.AimDx = 1
        p.AimDy = 0
        p.OnGround = True
        p.Alive = True
        p.WebLevel = 0
        p.InvulnTicks = RESPAWN_INVULN_TICKS
        p.ShootCooldown = 0
        p.RespawnTimer = 0
        p.IsSwinging = False
        Return p
    End Function

    ' Thiet ke man 1: duong pho (nen dat) + noc nha (san bay) + diem neo de du day to.
    Private Sub BuildLevel1()
        Platforms.Clear()
        Anchors.Clear()
        spawnDefs.Clear()

        Dim groundSegments As Double()() = New Double()() {
            New Double() {0, 1680},
            New Double() {1800, 3120},
            New Double() {3300, 5040},
            New Double() {5220, LEVEL_WIDTH_PX}
        }
        For Each seg In groundSegments
            Dim rect As New PlatformRect()
            rect.X = seg(0)
            rect.Y = GROUND_Y
            rect.W = seg(1) - seg(0)
            rect.H = 68
            rect.Kind = PlatformKind.Ground
            Platforms.Add(rect)
        Next

        ' Noc nha (san bay) - vua la cho dung, vua la mo cao gan diem neo
        AddOneWay(600, 360, 192)
        AddOneWay(1080, 250, 192)
        AddOneWay(2280, 360, 240)
        AddOneWay(3720, 300, 216)
        AddOneWay(4200, 230, 192)
        AddOneWay(5460, 340, 220)
        AddOneWay(6120, 260, 220)

        ' Diem neo de bam day to: dat cao hon noc nha mot chut, rai doc theo man
        AddAnchor(500, 140)
        AddAnchor(900, 110)
        AddAnchor(1300, 150)
        AddAnchor(1980, 130)
        AddAnchor(2460, 160)
        AddAnchor(2900, 120)
        AddAnchor(3500, 140)
        AddAnchor(4000, 110)
        AddAnchor(4500, 150)
        AddAnchor(4950, 130)
        AddAnchor(5350, 150)
        AddAnchor(5850, 120)
        AddAnchor(6300, 140)
        AddAnchor(6700, 130)

        ' Quai xuat hien theo tien do camera
        AddSpawn(360, 840, GROUND_Y - THUG_H, EnemyType.Thug, 1, 720, 1080)
        AddSpawn(840, 1200, GROUND_Y - THUG_H, EnemyType.Thug, 1, 1140, 1560)
        AddSpawn(1440, 1620, 250, EnemyType.Sniper, 2, 1620, 1620)
        AddSpawn(2160, 2400, GROUND_Y - THUG_H, EnemyType.Thug, 1, 2340, 2760)
        AddSpawn(2640, 2880, GROUND_Y - THUG_H, EnemyType.Thug, 1, 2820, 3120)
        AddSpawn(3480, 3660, 300, EnemyType.Sniper, 2, 3660, 3660)
        AddSpawn(4080, 4320, GROUND_Y - THUG_H, EnemyType.Thug, 1, 4260, 4680)
        AddSpawn(5400, 5640, GROUND_Y - THUG_H, EnemyType.Thug, 1, 5580, 6000)
        AddSpawn(5900, 6120, 260, EnemyType.Sniper, 2, 6120, 6120)
        ' Trum cuoi man
        AddSpawn(6960, 7200, GROUND_Y - BOSS_H, EnemyType.Boss, 14, 7200, 7200)

        PowerUps.Clear()
        AddPowerUp(780, GROUND_Y - 45, PowerUpType.WebUp)
        AddPowerUp(2460, GROUND_Y - 45, PowerUpType.WebUp)
        AddPowerUp(3780, 260, PowerUpType.LifeUp)
        AddPowerUp(5520, GROUND_Y - 45, PowerUpType.WebUp)
    End Sub

    Private Sub AddOneWay(x As Double, y As Double, w As Double)
        Dim rect As New PlatformRect()
        rect.X = x
        rect.Y = y
        rect.W = w
        rect.H = 16
        rect.Kind = PlatformKind.OneWay
        Platforms.Add(rect)
    End Sub

    Private Sub AddAnchor(x As Double, y As Double)
        Dim a As New SwingAnchor()
        a.X = x
        a.Y = y
        Anchors.Add(a)
    End Sub

    Private Sub AddSpawn(camTrigger As Double, x As Double, y As Double, kind As EnemyType, hp As Integer, patrolMin As Double, patrolMax As Double)
        Dim d As New EnemySpawnDef()
        d.SpawnAtCamX = camTrigger
        d.X = x
        d.Y = y
        d.Kind = kind
        d.HP = hp
        d.PatrolMinX = patrolMin
        d.PatrolMaxX = patrolMax
        d.Used = False
        spawnDefs.Add(d)
    End Sub

    Private Sub AddPowerUp(x As Double, y As Double, kind As PowerUpType)
        Dim p As New PowerUpState()
        p.X = x
        p.Y = y
        p.Kind = kind
        p.Active = True
        p.TtlTicks = -1
        PowerUps.Add(p)
    End Sub

    ' ===================== INPUT =====================
    Public Sub SetInput(playerIndex As Integer, inp As PlayerInput)
        If playerIndex < 0 OrElse playerIndex > 1 Then Return
        inputs(playerIndex) = inp
    End Sub

    ' ===================== VONG LAP CHINH =====================
    Public Sub Tick()
        If GameOver OrElse Victory Then Return
        tickCount += 1

        For i As Integer = 0 To 1
            If SoloMode AndAlso i = 1 Then Continue For
            UpdatePlayer(i)
        Next

        UpdateBullets()
        UpdateEnemies()
        UpdatePowerUps()
        CheckSpawns()
        UpdateCamera()
        CheckWinLose()
    End Sub

    Private Sub UpdatePlayer(idx As Integer)
        Dim p As PlayerState = Players(idx)
        Dim inp As PlayerInput = inputs(idx)

        If Not p.Alive Then
            If p.RespawnTimer > 0 Then
                p.RespawnTimer -= 1
                If p.RespawnTimer = 0 Then
                    Dim spawnX As Double = Math.Max(CameraX + 40, 40)
                    p.X = spawnX
                    p.Y = GROUND_Y - PLAYER_H
                    p.VelX = 0
                    p.VelY = 0
                    p.IsSwinging = False
                    p.Alive = True
                    p.InvulnTicks = RESPAWN_INVULN_TICKS
                    p.WebLevel = 0
                End If
            End If
            Players(idx) = p
            Return
        End If

        Dim moveX As Double = 0
        If inp.Left Then moveX -= MOVE_SPEED
        If inp.Right Then moveX += MOVE_SPEED
        If moveX > 0 Then p.FacingRight = True
        If moveX < 0 Then p.FacingRight = False
        p.IsMoving = (moveX <> 0)

        Dim aimDx As Integer = 0
        Dim aimDy As Integer = 0
        If inp.Up Then aimDy = -1
        If inp.Down Then aimDy = 1
        If inp.Left Then aimDx = -1
        If inp.Right Then aimDx = 1
        If aimDx = 0 AndAlso aimDy = 0 Then
            aimDx = If(p.FacingRight, 1, -1)
        End If
        p.AimDx = aimDx
        p.AimDy = aimDy

        If p.IsSwinging Then
            ' --- Vat ly du day to (con lac don gian) ---
            Dim pump As Double = 0
            If inp.Left Then pump -= SWING_PUMP_ACCEL
            If inp.Right Then pump += SWING_PUMP_ACCEL

            Dim angularAccel As Double = -(GRAVITY / p.SwingLength) * Math.Sin(p.SwingAngle)
            p.SwingAngularVel += angularAccel + pump
            p.SwingAngularVel *= SWING_DAMPING
            p.SwingAngle += p.SwingAngularVel

            Dim newSwingX As Double = p.SwingAnchorX + p.SwingLength * Math.Sin(p.SwingAngle) - PLAYER_W / 2.0
            Dim newSwingY As Double = p.SwingAnchorY + p.SwingLength * Math.Cos(p.SwingAngle) - PLAYER_H / 2.0

            p.VelX = newSwingX - p.X
            p.VelY = newSwingY - p.Y
            p.X = Math.Max(0, Math.Min(newSwingX, CDbl(LEVEL_WIDTH_PX - PLAYER_W)))
            p.Y = newSwingY
            p.OnGround = False

            ' Nha day to: tha phim Swing, hoac bam Nhay de "bat" ra voi luc day them
            If Not inp.Swing Then
                p.IsSwinging = False
            ElseIf inp.Jump Then
                p.IsSwinging = False
                p.VelY -= SWING_RELEASE_BOOST
            End If
        Else
            ' --- Nhay tu mat dat ---
            If inp.Jump AndAlso p.OnGround Then
                p.VelY = JUMP_VELOCITY
                p.OnGround = False
            End If

            ' --- Bam day to khi dang o tren khong va giu phim Swing ---
            If inp.Swing AndAlso Not p.OnGround Then
                TryAttachSwing(p)
            End If

            If Not p.IsSwinging Then
                p.VelY += GRAVITY
                If p.VelY > MAX_FALL_SPEED Then p.VelY = MAX_FALL_SPEED

                Dim newX As Double = p.X + moveX
                If Not p.OnGround Then
                    newX += p.VelX
                    p.VelX *= AIR_DAMPING
                Else
                    p.VelX = 0
                End If
                Dim newY As Double = p.Y + p.VelY

                newX = Math.Max(0, Math.Min(newX, CDbl(LEVEL_WIDTH_PX - PLAYER_W)))

                ResolvePlatformCollision(newX, newY, p)
            End If
        End If

        If p.ShootCooldown > 0 Then p.ShootCooldown -= 1
        If inp.Shoot AndAlso p.ShootCooldown = 0 Then
            FireWeb(idx, p)
            p.ShootCooldown = CooldownForLevel(p.WebLevel)
        End If

        If p.InvulnTicks > 0 Then p.InvulnTicks -= 1

        Players(idx) = p
    End Sub

    ' Tim diem neo gan nhat, o phia truoc/tren nguoi choi va trong tam bay to, roi bam day vao do
    Private Sub TryAttachSwing(ByRef p As PlayerState)
        Dim centerX As Double = p.X + PLAYER_W / 2.0
        Dim centerY As Double = p.Y + PLAYER_H / 2.0

        Dim bestDist As Double = Double.MaxValue
        Dim bestX As Double = 0
        Dim bestY As Double = 0
        Dim found As Boolean = False

        For Each a In Anchors
            If a.Y >= centerY Then Continue For ' diem neo phai o phia tren nguoi choi
            Dim dx As Double = a.X - centerX
            Dim dy As Double = a.Y - centerY
            Dim dist As Double = Math.Sqrt(dx * dx + dy * dy)
            If dist < SWING_MIN_LEN OrElse dist > SWING_MAX_LEN Then Continue For
            If dist < bestDist Then
                bestDist = dist
                bestX = a.X
                bestY = a.Y
                found = True
            End If
        Next

        If Not found Then Return

        p.IsSwinging = True
        p.SwingAnchorX = bestX
        p.SwingAnchorY = bestY
        p.SwingLength = bestDist
        p.SwingAngle = Math.Atan2(centerX - bestX, centerY - bestY)
        ' Khoi tao van toc goc tu da hien tai de chuyen dong khong bi giat cuc
        p.SwingAngularVel = 0
    End Sub

    ' Public de Form1.vb co the doc gia tri cooldown toi da, dung phat hien "vua ban"
    ' (de chon khung hinh "ban giua khong" khi ShootCooldown moi duoc dat lai ve max).
    Public Function CooldownForLevel(level As Integer) As Integer
        Select Case level
            Case 1 : Return RAPID_COOLDOWN
            Case 2 : Return SPREAD_COOLDOWN
            Case Else : Return NORMAL_COOLDOWN
        End Select
    End Function

    Private Sub ResolvePlatformCollision(newX As Double, newY As Double, ByRef p As PlayerState)
        p.X = newX
        Dim landed As Boolean = False

        For Each plat In Platforms
            Dim withinX As Boolean = (p.X + PLAYER_W > plat.X) AndAlso (p.X < plat.X + plat.W)
            If Not withinX Then Continue For

            Dim playerBottomOld As Double = p.Y + PLAYER_H
            Dim playerBottomNew As Double = newY + PLAYER_H

            If p.VelY >= 0 AndAlso playerBottomOld <= plat.Y + 4 AndAlso playerBottomNew >= plat.Y Then
                newY = plat.Y - PLAYER_H
                p.VelY = 0
                landed = True
            End If
        Next

        p.Y = newY
        p.OnGround = landed
        If landed Then p.VelX = 0

        If p.Y > VIEW_HEIGHT_PX + 200 Then
            KillPlayer(p)
        End If
    End Sub

    Private Sub KillPlayer(ByRef p As PlayerState)
        If Not p.Alive Then Return
        p.Alive = False
        p.RespawnTimer = 60
        p.IsSwinging = False
        SharedLives -= 1
    End Sub

    Private Sub FireWeb(idx As Integer, p As PlayerState)
        Dim originX As Double = p.X + PLAYER_W / 2.0
        Dim originY As Double = p.Y + PLAYER_H / 2.0

        Select Case p.WebLevel
            Case 2
                SpawnBullet(originX, originY, p.AimDx, p.AimDy, idx)
                SpawnWebSpread(originX, originY, p.AimDx, p.AimDy, idx)
            Case Else
                SpawnBullet(originX, originY, p.AimDx, p.AimDy, idx)
        End Select
    End Sub

    Private Sub SpawnWebSpread(x As Double, y As Double, dx As Integer, dy As Integer, owner As Integer)
        If dy = 0 Then
            SpawnBullet(x, y, dx, -1, owner)
            SpawnBullet(x, y, dx, 1, owner)
        Else
            SpawnBullet(x, y, dx, dy, owner)
        End If
    End Sub

    Private Sub SpawnBullet(x As Double, y As Double, dx As Integer, dy As Integer, owner As Integer)
        Dim len As Double = Math.Sqrt(CDbl(dx * dx + dy * dy))
        If len = 0 Then len = 1
        Dim b As New BulletState()
        b.X = x
        b.Y = y
        b.DirX = (CDbl(dx) / len) * WEB_SPEED
        b.DirY = (CDbl(dy) / len) * WEB_SPEED
        b.Owner = owner
        b.Active = True
        Bullets.Add(b)
    End Sub

    Private Sub UpdateBullets()
        For i As Integer = Bullets.Count - 1 To 0 Step -1
            Dim b As BulletState = Bullets(i)
            If Not b.Active Then
                Bullets.RemoveAt(i)
                Continue For
            End If

            b.X += b.DirX
            b.Y += b.DirY

            Dim screenX As Double = b.X - CameraX
            If screenX < -60 OrElse screenX > VIEW_WIDTH_PX + 60 OrElse b.Y < -60 OrElse b.Y > VIEW_HEIGHT_PX + 60 Then
                b.Active = False
                Bullets(i) = b
                Continue For
            End If

            If b.Owner >= 0 Then
                ' Tơ cua nguoi choi: kiem tra trung quai
                For ei As Integer = 0 To Enemies.Count - 1
                    Dim en As EnemyState = Enemies(ei)
                    If Not en.Alive Then Continue For
                    Dim ew As Integer = If(en.Kind = EnemyType.Boss, BOSS_W, If(en.Kind = EnemyType.Sniper, SNIPER_W, THUG_W))
                    Dim eh As Integer = If(en.Kind = EnemyType.Boss, BOSS_H, If(en.Kind = EnemyType.Sniper, SNIPER_H, THUG_H))
                    If RectHit(b.X, b.Y, en.X, en.Y, ew, eh) Then
                        en.HP -= 1
                        If en.HP <= 0 Then en.Alive = False
                        Enemies(ei) = en
                        b.Active = False
                        Exit For
                    End If
                Next
            Else
                ' Tơ/dan cua quai: kiem tra trung nguoi choi
                For pi As Integer = 0 To 1
                    Dim pl As PlayerState = Players(pi)
                    If Not pl.Alive OrElse pl.InvulnTicks > 0 Then Continue For
                    If RectHit(b.X, b.Y, pl.X, pl.Y, PLAYER_W, PLAYER_H) Then
                        KillPlayer(pl)
                        Players(pi) = pl
                        b.Active = False
                        Exit For
                    End If
                Next
            End If

            Bullets(i) = b
        Next
    End Sub

    Private Function RectHit(px As Double, py As Double, rx As Double, ry As Double, rw As Double, rh As Double) As Boolean
        Return px >= rx AndAlso px <= rx + rw AndAlso py >= ry AndAlso py <= ry + rh
    End Function

    Private Sub UpdateEnemies()
        For i As Integer = 0 To Enemies.Count - 1
            Dim en As EnemyState = Enemies(i)
            If Not en.Alive Then Continue For

            Select Case en.Kind
                Case EnemyType.Thug
                    en.X += en.VelX
                    If en.X <= en.PatrolMinX OrElse en.X >= en.PatrolMaxX Then
                        en.VelX = -en.VelX
                    End If
                    If en.VelX <> 0 Then en.FacingRight = (en.VelX > 0)
                Case EnemyType.Sniper
                    Dim tp As PlayerState = NearestAlivePlayer(en.X, en.Y)
                    If tp.Alive Then
                        en.FacingRight = (tp.X >= en.X)
                        Dim ddx As Double = (tp.X + PLAYER_W / 2.0) - en.X
                        Dim ddy As Double = (tp.Y + PLAYER_H / 2.0) - en.Y
                        en.AimAngleDeg = Math.Atan2(ddy, ddx) * (180.0 / Math.PI)
                    End If
                Case EnemyType.Boss
                    en.X += en.VelX
                    If en.X <= en.PatrolMinX - 100 OrElse en.X >= en.PatrolMaxX + 100 Then
                        en.VelX = -en.VelX
                    End If
                    If en.VelX <> 0 Then en.FacingRight = (en.VelX > 0)
            End Select

            If en.ShootCooldown > 0 Then
                en.ShootCooldown -= 1
            Else
                Dim target As PlayerState = NearestAlivePlayer(en.X, en.Y)
                If target.Alive Then
                    Dim ddx As Double = target.X - en.X
                    Dim ddy As Double = target.Y - en.Y
                    SpawnBullet(en.X, en.Y, Math.Sign(ddx), Math.Sign(ddy), -1)
                    en.ShootCooldown = If(en.Kind = EnemyType.Boss, 25, 55)
                End If
            End If

            Enemies(i) = en
        Next
    End Sub

    Private Function NearestAlivePlayer(x As Double, y As Double) As PlayerState
        Dim best As PlayerState = Players(0)
        Dim bestDist As Double = Double.MaxValue
        Dim found As Boolean = False
        For i As Integer = 0 To 1
            If Players(i).Alive Then
                Dim d As Double = Math.Abs(Players(i).X - x)
                If d < bestDist Then
                    bestDist = d
                    best = Players(i)
                    found = True
                End If
            End If
        Next
        If Not found Then
            Dim dead As New PlayerState()
            dead.Alive = False
            Return dead
        End If
        Return best
    End Function

    Private Sub UpdatePowerUps()
        For i As Integer = PowerUps.Count - 1 To 0 Step -1
            Dim pu As PowerUpState = PowerUps(i)
            If Not pu.Active Then
                PowerUps.RemoveAt(i)
                Continue For
            End If
            If pu.TtlTicks > 0 Then
                pu.TtlTicks -= 1
                If pu.TtlTicks = 0 Then pu.Active = False
            End If

            For pi As Integer = 0 To 1
                Dim pl As PlayerState = Players(pi)
                If Not pl.Alive Then Continue For
                If RectHit(pu.X, pu.Y, pl.X, pl.Y, PLAYER_W, PLAYER_H) Then
                    ApplyPowerUp(pl, pu.Kind)
                    Players(pi) = pl
                    pu.Active = False
                End If
            Next

            PowerUps(i) = pu
        Next
    End Sub

    Private Sub ApplyPowerUp(ByRef p As PlayerState, kind As PowerUpType)
        Select Case kind
            Case PowerUpType.WebUp
                If p.WebLevel < 2 Then p.WebLevel += 1
            Case PowerUpType.LifeUp
                SharedLives += 1
        End Select
    End Sub

    Private Sub CheckSpawns()
        If tickCount Mod ENEMY_SPAWN_CHECK_TICKS <> 0 Then Return
        Dim aliveCount As Integer = 0
        For Each en In Enemies
            If en.Alive Then aliveCount += 1
        Next
        If aliveCount >= ENEMY_MAX_ALIVE Then Return

        For i As Integer = 0 To spawnDefs.Count - 1
            Dim d As EnemySpawnDef = spawnDefs(i)
            If d.Used Then Continue For
            If CameraX + VIEW_WIDTH_PX >= d.SpawnAtCamX Then
                Dim en As New EnemyState()
                en.X = d.X
                en.Y = d.Y
                en.Kind = d.Kind
                en.HP = d.HP
                en.Alive = True
                en.FacingRight = True
                en.ShootCooldown = 30
                en.PatrolMinX = d.PatrolMinX
                en.PatrolMaxX = d.PatrolMaxX
                en.VelX = If(d.Kind = EnemyType.Thug, 1.44, 0.96)
                Enemies.Add(en)
                d.Used = True
                spawnDefs(i) = d
            End If
        Next
    End Sub

    Private Sub UpdateCamera()
        Dim maxX As Double = CameraX
        For i As Integer = 0 To 1
            If Players(i).Alive Then
                Dim desired As Double = Players(i).X - 200
                If desired > maxX Then maxX = desired
            End If
        Next
        maxX = Math.Min(maxX, CDbl(LEVEL_WIDTH_PX - VIEW_WIDTH_PX))
        CameraX = Math.Max(CameraX, Math.Max(0.0, maxX))

        For i As Integer = 0 To 1
            Dim p As PlayerState = Players(i)
            If p.Alive AndAlso p.X < CameraX Then
                p.X = CameraX
                Players(i) = p
            End If
        Next
    End Sub

    Private Sub CheckWinLose()
        If SharedLives <= 0 Then
            Dim bothDead As Boolean
            If SoloMode Then
                bothDead = Not Players(0).Alive
            Else
                bothDead = (Not Players(0).Alive) AndAlso (Not Players(1).Alive)
            End If
            If bothDead Then GameOver = True
            Return
        End If

        Dim bossDead As Boolean = True
        Dim bossExists As Boolean = False
        For Each en In Enemies
            If en.Kind = EnemyType.Boss Then
                bossExists = True
                If en.Alive Then bossDead = False
            End If
        Next
        If bossExists AndAlso bossDead Then Victory = True
    End Sub

    ' ===================== SERIALIZE / DESERIALIZE (giao thuc mang) =====================
    Public Function SerializeState() As String
        Dim sb As New StringBuilder()
        sb.Append("STATE|")
        sb.Append(CameraX.ToString("F1")).Append("|")
        sb.Append(SharedLives.ToString()).Append("|")
        sb.Append(If(GameOver, "1", "0")).Append("|")
        sb.Append(If(Victory, "1", "0")).Append("|")
        sb.Append(SerializePlayer(Players(0))).Append("|")
        sb.Append(SerializePlayer(Players(1))).Append("|")

        sb.Append(Bullets.Count.ToString()).Append("|")
        For i As Integer = 0 To Bullets.Count - 1
            If i > 0 Then sb.Append(";")
            Dim b As BulletState = Bullets(i)
            sb.Append(String.Format("{0:F1},{1:F1},{2:F1},{3:F1},{4}", b.X, b.Y, b.DirX, b.DirY, b.Owner))
        Next
        sb.Append("|")

        sb.Append(Enemies.Count.ToString()).Append("|")
        For i As Integer = 0 To Enemies.Count - 1
            If i > 0 Then sb.Append(";")
            Dim en As EnemyState = Enemies(i)
            sb.Append(String.Format("{0:F1},{1:F1},{2},{3},{4},{5},{6:F1}", en.X, en.Y, CInt(en.Kind), en.HP, If(en.Alive, 1, 0), If(en.FacingRight, 1, 0), en.AimAngleDeg))
        Next
        sb.Append("|")

        sb.Append(PowerUps.Count.ToString()).Append("|")
        For i As Integer = 0 To PowerUps.Count - 1
            If i > 0 Then sb.Append(";")
            Dim pu As PowerUpState = PowerUps(i)
            sb.Append(String.Format("{0:F1},{1:F1},{2}", pu.X, pu.Y, CInt(pu.Kind)))
        Next

        Return sb.ToString()
    End Function

    Private Function SerializePlayer(p As PlayerState) As String
        Return String.Format("{0:F1},{1:F1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
            p.X, p.Y,
            If(p.FacingRight, 1, 0),
            p.AimDx, p.AimDy,
            If(p.OnGround, 1, 0),
            If(p.Alive, 1, 0),
            p.WebLevel,
            p.InvulnTicks,
            p.RespawnTimer,
            If(p.IsMoving, 1, 0),
            If(p.IsSwinging, 1, 0),
            p.SkinIndex)
    End Function

    Public Sub ApplyStateLine(line As String)
        Dim parts As String() = line.Split("|"c)
        If parts.Length < 10 Then Return
        If parts(0) <> "STATE" Then Return

        CameraX = Double.Parse(parts(1), Globalization.CultureInfo.InvariantCulture)
        SharedLives = Integer.Parse(parts(2))
        GameOver = (parts(3) = "1")
        Victory = (parts(4) = "1")
        Players(0) = ParsePlayer(parts(5))
        Players(1) = ParsePlayer(parts(6))

        Dim nBullets As Integer = Integer.Parse(parts(7))
        Bullets.Clear()
        If nBullets > 0 Then
            Dim items As String() = parts(8).Split(";"c)
            For Each item In items
                Bullets.Add(ParseBullet(item))
            Next
        End If

        Dim nEnemies As Integer = Integer.Parse(parts(9))
        Enemies.Clear()
        If nEnemies > 0 AndAlso parts.Length > 10 Then
            Dim items As String() = parts(10).Split(";"c)
            For Each item In items
                Enemies.Add(ParseEnemy(item))
            Next
        End If

        If parts.Length > 12 Then
            Dim nPowerups As Integer = Integer.Parse(parts(11))
            PowerUps.Clear()
            If nPowerups > 0 Then
                Dim items As String() = parts(12).Split(";"c)
                For Each item In items
                    PowerUps.Add(ParsePowerUp(item))
                Next
            End If
        End If
    End Sub

    Private Function ParsePlayer(s As String) As PlayerState
        Dim f As String() = s.Split(","c)
        Dim p As New PlayerState()
        p.X = Double.Parse(f(0), Globalization.CultureInfo.InvariantCulture)
        p.Y = Double.Parse(f(1), Globalization.CultureInfo.InvariantCulture)
        p.FacingRight = (f(2) = "1")
        p.AimDx = Integer.Parse(f(3))
        p.AimDy = Integer.Parse(f(4))
        p.OnGround = (f(5) = "1")
        p.Alive = (f(6) = "1")
        p.WebLevel = Integer.Parse(f(7))
        p.InvulnTicks = Integer.Parse(f(8))
        p.RespawnTimer = Integer.Parse(f(9))
        p.IsMoving = If(f.Length > 10, f(10) = "1", False)
        p.IsSwinging = If(f.Length > 11, f(11) = "1", False)
        p.SkinIndex = If(f.Length > 12, Integer.Parse(f(12)), 0)
        Return p
    End Function

    Private Function ParseBullet(s As String) As BulletState
        Dim f As String() = s.Split(","c)
        Dim b As New BulletState()
        b.X = Double.Parse(f(0), Globalization.CultureInfo.InvariantCulture)
        b.Y = Double.Parse(f(1), Globalization.CultureInfo.InvariantCulture)
        b.DirX = Double.Parse(f(2), Globalization.CultureInfo.InvariantCulture)
        b.DirY = Double.Parse(f(3), Globalization.CultureInfo.InvariantCulture)
        b.Owner = Integer.Parse(f(4))
        b.Active = True
        Return b
    End Function

    Private Function ParseEnemy(s As String) As EnemyState
        Dim f As String() = s.Split(","c)
        Dim en As New EnemyState()
        en.X = Double.Parse(f(0), Globalization.CultureInfo.InvariantCulture)
        en.Y = Double.Parse(f(1), Globalization.CultureInfo.InvariantCulture)
        en.Kind = CType(Integer.Parse(f(2)), EnemyType)
        en.HP = Integer.Parse(f(3))
        en.Alive = (f(4) = "1")
        en.FacingRight = If(f.Length > 5, f(5) = "1", True)
        en.AimAngleDeg = If(f.Length > 6, Double.Parse(f(6), Globalization.CultureInfo.InvariantCulture), 0.0)
        Return en
    End Function

    Private Function ParsePowerUp(s As String) As PowerUpState
        Dim f As String() = s.Split(","c)
        Dim pu As New PowerUpState()
        pu.X = Double.Parse(f(0), Globalization.CultureInfo.InvariantCulture)
        pu.Y = Double.Parse(f(1), Globalization.CultureInfo.InvariantCulture)
        pu.Kind = CType(Integer.Parse(f(2)), PowerUpType)
        pu.Active = True
        Return pu
    End Function

    Public Shared Function SerializeInput(inp As PlayerInput) As String
        Return String.Format("INPUT|{0}|{1}|{2}|{3}|{4}|{5}|{6}",
            If(inp.Left, 1, 0), If(inp.Right, 1, 0),
            If(inp.Up, 1, 0), If(inp.Down, 1, 0),
            If(inp.Jump, 1, 0), If(inp.Shoot, 1, 0), If(inp.Swing, 1, 0))
    End Function

    Public Shared Function ParseInput(line As String) As PlayerInput
        Dim inp As New PlayerInput()
        Dim parts As String() = line.Split("|"c)
        If parts.Length < 7 OrElse parts(0) <> "INPUT" Then Return inp
        inp.Left = (parts(1) = "1")
        inp.Right = (parts(2) = "1")
        inp.Up = (parts(3) = "1")
        inp.Down = (parts(4) = "1")
        inp.Jump = (parts(5) = "1")
        inp.Shoot = (parts(6) = "1")
        inp.Swing = If(parts.Length > 7, parts(7) = "1", False)
        Return inp
    End Function

End Class
