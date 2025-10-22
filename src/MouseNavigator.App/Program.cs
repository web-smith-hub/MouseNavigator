using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace MouseNavigator.App
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplication());
        }
    }

    public class TrayApplication : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly SettingsForm _settings;
        private readonly EdgeArrowOverlay _overlay;
        private readonly CursorWatcher _watcher;

        public TrayApplication()
        {
            _settings = new SettingsForm();
            _overlay = new EdgeArrowOverlay(_settings.Settings);
            _watcher = new CursorWatcher(_overlay, _settings.Settings);

            var menu = new ContextMenuStrip();
            var settingsItem = new ToolStripMenuItem("Настройки", null, (_, __) => _settings.ShowDialog());
            var exitItem = new ToolStripMenuItem("Выход", null, (_, __) => ExitThread());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _tray = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Visible = true,
                Text = "Mouse Navigator - Навигатор курсора мыши",
                ContextMenuStrip = menu
            };

            _settings.SettingsChanged += (_, __) => _overlay.ApplySettings(_settings.Settings);
            Application.ApplicationExit += (_, __) => { _tray.Visible = false; _tray.Dispose(); };
        }

        private Icon CreateTrayIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(70, 130, 180)))
                using (var pen = new Pen(Color.FromArgb(25, 25, 112), 2))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                    // Стрелка указателя
                    g.DrawLine(pen, 8, 4, 8, 12);
                    g.DrawLine(pen, 8, 4, 6, 6);
                    g.DrawLine(pen, 8, 4, 10, 6);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }
    }

    public class AppSettings
    {
        public float ArrowScale { get; set; } = 1.0f;
        public int EdgeZonePx { get; set; } = 10;
        public int SpeedBoostPercent { get; set; } = 40;
        public int SpeedThreshold { get; set; } = 1400;
        public int AnimationFps { get; set; } = 60;
        public int PulseFrequency { get; set; } = 0;
        public int OpacityPercent { get; set; } = 85;
        public bool EnableAnimations { get; set; } = true;
        public Color ArrowColor { get; set; } = Color.FromArgb(90, 160, 255);
        public AnimationType AnimationType { get; set; } = AnimationType.Pulse;
    }

    public enum AnimationType
    {
        None,
        Pulse,
        Glow,
        Bounce
    }

    public class SettingsForm : Form
    {
        public AppSettings Settings { get; } = new();
        public event EventHandler? SettingsChanged;

        public SettingsForm()
        {
            InitializeForm();
            CreateControls();
        }

        private void InitializeForm()
        {
            Text = "Настройки Mouse Navigator";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(460, 420);
            BackColor = Color.FromArgb(240, 240, 240);
        }

        private void CreateControls()
        {
            var y = 20;
            var spacing = 40;

            AddSettingControl("Размер стрелки (%)", 50, 300, 100, (int)(Settings.ArrowScale * 100), ref y, spacing,
                (value) => Settings.ArrowScale = value / 100f);

            AddSettingControl("Зона края (пиксели)", 1, 50, Settings.EdgeZonePx, Settings.EdgeZonePx, ref y, spacing,
                (value) => Settings.EdgeZonePx = value);

            AddSettingControl("Увеличение при скорости (%)", 0, 200, Settings.SpeedBoostPercent, Settings.SpeedBoostPercent, ref y, spacing,
                (value) => Settings.SpeedBoostPercent = value);

            AddSettingControl("Порог скорости (пикс/сек)", 100, 5000, Settings.SpeedThreshold, Settings.SpeedThreshold, ref y, spacing,
                (value) => Settings.SpeedThreshold = value);

            AddSettingControl("FPS анимации", 15, 120, Settings.AnimationFps, Settings.AnimationFps, ref y, spacing,
                (value) => Settings.AnimationFps = value);

            AddSettingControl("Частота пульсации (Гц)", 0, 10, Settings.PulseFrequency, Settings.PulseFrequency, ref y, spacing,
                (value) => Settings.PulseFrequency = value);

            AddSettingControl("Прозрачность (%)", 20, 100, Settings.OpacityPercent, Settings.OpacityPercent, ref y, spacing,
                (value) => Settings.OpacityPercent = value);

            // Кнопка применить
            var btnApply = new Button
            {
                Text = "Применить настройки",
                Location = new Point(20, y + 20),
                Size = new Size(200, 35),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += (_, __) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            Controls.Add(btnApply);
        }

        private void AddSettingControl(string label, int min, int max, int defaultValue, int currentValue, ref int y, int spacing, Action<int> onValueChanged)
        {
            var lblSetting = new Label
            {
                Text = label,
                Location = new Point(20, y),
                Size = new Size(220, 23),
                Font = new Font("Segoe UI", 9)
            };

            var numSetting = new NumericUpDown
            {
                Location = new Point(250, y - 2),
                Size = new Size(120, 23),
                Minimum = min,
                Maximum = max,
                Value = currentValue
            };

            numSetting.ValueChanged += (_, __) => onValueChanged((int)numSetting.Value);

            Controls.Add(lblSetting);
            Controls.Add(numSetting);
            y += spacing;
        }
    }

    public class CursorWatcher
    {
        private readonly EdgeArrowOverlay _overlay;
        private readonly AppSettings _settings;
        private Point _lastPosition;
        private DateTime _lastTime;
        private readonly Timer _timer;

        public CursorWatcher(EdgeArrowOverlay overlay, AppSettings settings)
        {
            _overlay = overlay;
            _settings = settings;
            _lastPosition = Cursor.Position;
            _lastTime = DateTime.UtcNow;

            _timer = new Timer { Interval = 1000 / Math.Max(30, settings.AnimationFps) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var currentPos = Cursor.Position;
            var currentTime = DateTime.UtcNow;
            var deltaTime = Math.Max(0.001, (currentTime - _lastTime).TotalSeconds);
            var distance = Distance(_lastPosition, currentPos);
            var speed = distance / deltaTime;

            _lastPosition = currentPos;
            _lastTime = currentTime;

            var primaryScreen = Screen.PrimaryScreen!.Bounds;
            var isOnPrimary = primaryScreen.Contains(currentPos);

            if (!isOnPrimary)
            {
                // Курсор на другом мониторе
                var direction = GetDirectionFromPrimary(primaryScreen, currentPos);
                var edgePoint = GetEdgePoint(primaryScreen, direction);
                _overlay.Show(edgePoint, direction, speed);
            }
            else if (IsNearEdge(primaryScreen, currentPos, out var edgeDirection, out var edgePos))
            {
                // Курсор у края главного экрана
                _overlay.Show(edgePos, edgeDirection, speed);
            }
            else
            {
                _overlay.Hide();
            }
        }

        private static double Distance(Point a, Point b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static float GetDirectionFromPrimary(Rectangle primary, Point cursorPos)
        {
            var centerX = primary.X + primary.Width / 2f;
            var centerY = primary.Y + primary.Height / 2f;
            var dx = cursorPos.X - centerX;
            var dy = cursorPos.Y - centerY;
            return (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        private static Point GetEdgePoint(Rectangle primary, float angleDeg)
        {
            var centerX = primary.X + primary.Width / 2f;
            var centerY = primary.Y + primary.Height / 2f;
            var angleRad = angleDeg * Math.PI / 180.0;
            var dx = Math.Cos(angleRad);
            var dy = Math.Sin(angleRad);

            // Находим пересечение луча с границами прямоугольника
            var t1 = dx > 0 ? (primary.Right - 1 - centerX) / dx : (primary.X - centerX) / dx;
            var t2 = dy > 0 ? (primary.Bottom - 1 - centerY) / dy : (primary.Y - centerY) / dy;
            var t = Math.Min(Math.Abs(t1), Math.Abs(t2));

            return new Point(
                (int)(centerX + dx * t),
                (int)(centerY + dy * t)
            );
        }

        private bool IsNearEdge(Rectangle bounds, Point pos, out float direction, out Point edgePoint)
        {
            direction = 0f;
            edgePoint = Point.Empty;

            var distanceLeft = pos.X - bounds.Left;
            var distanceRight = bounds.Right - 1 - pos.X;
            var distanceTop = pos.Y - bounds.Top;
            var distanceBottom = bounds.Bottom - 1 - pos.Y;

            var minDistance = Math.Min(Math.Min(distanceLeft, distanceRight), Math.Min(distanceTop, distanceBottom));

            if (minDistance <= _settings.EdgeZonePx)
            {
                if (minDistance == distanceLeft)
                {
                    direction = 180f; // Влево
                    edgePoint = new Point(bounds.Left, pos.Y);
                }
                else if (minDistance == distanceRight)
                {
                    direction = 0f; // Вправо
                    edgePoint = new Point(bounds.Right - 1, pos.Y);
                }
                else if (minDistance == distanceTop)
                {
                    direction = -90f; // Вверх
                    edgePoint = new Point(pos.X, bounds.Top);
                }
                else
                {
                    direction = 90f; // Вниз
                    edgePoint = new Point(pos.X, bounds.Bottom - 1);
                }
                return true;
            }
            return false;
        }
    }

    public class EdgeArrowOverlay : Form
    {
        private float _currentAngle;
        private float _currentScale = 1f;
        private float _targetScale = 1f;
        private float _animationPhase;
        private readonly Timer _animationTimer;
        private AppSettings _settings;
        private Point _arrowPosition;

        public EdgeArrowOverlay(AppSettings settings)
        {
            _settings = settings;
            InitializeOverlay();

            _animationTimer = new Timer { Interval = 1000 / Math.Max(30, settings.AnimationFps) };
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();
        }

        private void InitializeOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen!.Bounds;
            BackColor = Color.Lime;
            TransparencyKey = Color.Lime;
            Visible = false;
            DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            _animationTimer.Interval = 1000 / Math.Max(30, settings.AnimationFps);
            Opacity = settings.OpacityPercent / 100.0;
        }

        public void Show(Point position, float angleDeg, double speed)
        {
            _arrowPosition = position;
            _currentAngle = angleDeg;
            
            var baseScale = _settings.ArrowScale;
            var speedBoost = speed >= _settings.SpeedThreshold ? (1f + _settings.SpeedBoostPercent / 100f) : 1f;
            _targetScale = baseScale * speedBoost;

            if (!Visible)
            {
                Visible = true;
            }
        }

        public void Hide()
        {
            if (Visible)
            {
                Visible = false;
            }
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (_settings.EnableAnimations)
            {
                _currentScale = Lerp(_currentScale, _targetScale, 0.15f);
                
                if (_settings.PulseFrequency > 0)
                {
                    _animationPhase += (float)(_settings.PulseFrequency * 2 * Math.PI / Math.Max(1, _settings.AnimationFps));
                }
            }
            else
            {
                _currentScale = _targetScale;
            }

            if (Visible)
            {
                Invalidate();
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Visible) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var pulse = _settings.PulseFrequency > 0 ? (float)(0.1 * Math.Sin(_animationPhase)) : 0f;
            var scale = _currentScale * (1f + pulse);
            var size = 40 * scale;

            // Создаем стрелку
            var arrowPath = CreateModernArrowPath(size);
            
            // Трансформируем стрелку
            var matrix = new Matrix();
            matrix.Translate(-size/2, 0); // Центрируем
            matrix.Rotate(_currentAngle);
            matrix.Translate(_arrowPosition.X, _arrowPosition.Y);
            arrowPath.Transform(matrix);

            // Рисуем тень
            var shadowMatrix = new Matrix();
            shadowMatrix.Translate(-size/2, 0);
            shadowMatrix.Rotate(_currentAngle);
            shadowMatrix.Translate(_arrowPosition.X + 3, _arrowPosition.Y + 3);
            var shadowPath = CreateModernArrowPath(size);
            shadowPath.Transform(shadowMatrix);
            using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                g.FillPath(shadowBrush, shadowPath);
            }

            // Рисуем стрелку
            using (var fillBrush = new LinearGradientBrush(
                new Rectangle((int)(_arrowPosition.X - size), (int)(_arrowPosition.Y - size/2), 
                             (int)(size * 2), (int)size),
                Color.FromArgb(255, _settings.ArrowColor),
                Color.FromArgb(180, _settings.ArrowColor),
                LinearGradientMode.Vertical))
            using (var outlinePen = new Pen(Color.FromArgb(200, 30, 60, 160), 2f))
            {
                g.FillPath(fillBrush, arrowPath);
                g.DrawPath(outlinePen, arrowPath);
            }

            arrowPath.Dispose();
            shadowPath.Dispose();
        }

        private static GraphicsPath CreateModernArrowPath(float size)
        {
            var path = new GraphicsPath();
            var points = new PointF[]
            {
                new PointF(0, -size/4),           // Верхняя точка стрелки
                new PointF(size, 0),              // Правая точка (кончик)
                new PointF(0, size/4),            // Нижняя точка стрелки
                new PointF(size/5, size/8),       // Внутренний угол снизу
                new PointF(-size*0.6f, size/8),   // Хвост снизу
                new PointF(-size*0.6f, -size/8),  // Хвост сверху
                new PointF(size/5, -size/8)       // Внутренний угол сверху
            };
            path.AddPolygon(points);
            return path;
        }
    }
}