using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Pybrary.Plot
{
    public class DateTimeAxis : Axis, XAxis
    {
        // priority of scales - zoomed, user, data, unscaled

        private DateTime? zoomedMinimum = null;
        private DateTime? zoomedMaximum = null;
        private DateTime? userMinimum = null;
        private DateTime? userMaximum = null;
        private DateTime unscaledMinimum = new DateTime(2000, 1, 1);
        private DateTime unscaledMaximum = new DateTime(2000, 12, 31);
        private float minorTickLength = 0.025f; // in inches
        private PenDescription minorTickPen = new PenDescription(Color.Black, 1f / 96);
        private FontDescription smallLabelFont = new FontDescription("Arial", 8f, FontStyle.Regular);

        private Plot parent;

        private enum AxisType {
            DailyWithHourlyLabels,
            DailyWithHourlyTicks,
            MonthsHorizontalWithDailyLabels,
            MonthsHorizontalWithDailyTicks,
            MonthsHorizontal,
            MonthsVertical,
            Quarters
        };
        private AxisType axisType = AxisType.MonthsHorizontalWithDailyTicks;

        private GregorianCalendar cal = new GregorianCalendar();

        public DateTimeAxis(Plot parent)
        {
            this.parent = parent;
        }

        public float CalculateHeight(Graphics g, float maximumWidth)
        {
            using (Font f = labelFont.CreateFont())
            using (Font f2 = smallLabelFont.CreateFont())
            {
                // Try different axis, from the smallest increments to the
                // largest increments, and check which fits.
                TimeSpan delta = ScaleMaximum - ScaleMinimum;
                int numLabels;
                SizeF labelSize;
                float estimatedWidth;

                // Check daily major labels.  Shortcut: avoid long
                // calculateNumLabels call if delta is too large to be likely.
                if (delta < new TimeSpan(10, 0, 0, 0))
                {
                    // Calculate number of days:
                    axisType = AxisType.DailyWithHourlyTicks;
                    numLabels = calculateNumLabels();

                    labelSize = g.MeasureString("Mar 12 '05", f);
                    estimatedWidth = (labelSize.Width + 0.3f) * numLabels;
                    if (estimatedWidth < maximumWidth)
                    {
                        // Daily major labels.
                        float height = labelSize.Height + tickLength;

                        // Check for whether hourly labels might fit
                        labelSize = g.MeasureString("12P", f2);
                        double numHours = (ScaleMaximum - ScaleMinimum).TotalHours;
                        if ((1.5 * numHours * labelSize.Width) < maximumWidth)
                        {
                            axisType = AxisType.DailyWithHourlyLabels;
                            height += 1f / 16;
                        }
                        else
                            axisType = AxisType.DailyWithHourlyTicks;

                        return height;
                    }
                }

                // Horizontal months labels.
                axisType = AxisType.MonthsHorizontalWithDailyLabels;
                numLabels = calculateNumLabels();
                labelSize = g.MeasureString("Mar '05", f);
                estimatedWidth = (labelSize.Width + 0.3f) * numLabels;
                if (estimatedWidth < maximumWidth)
                {
                    // monthly horizontal labels
                    float height = labelSize.Height + tickLength;

                    SizeF dayLabelSize = g.MeasureString("30", f2);

                    // do we also have room for daily ticks?
                    double numDays = (ScaleMaximum - ScaleMinimum).TotalDays;
                    if (numDays * dayLabelSize.Width < maximumWidth)
                    {
                        axisType = AxisType.MonthsHorizontalWithDailyLabels;
                        height += 1f / 16;
                    }
                    else if ((0.05f * numDays) < maximumWidth)
                        axisType = AxisType.MonthsHorizontalWithDailyTicks;
                    else
                        axisType = AxisType.MonthsHorizontal;

                    return height;
                }

                // OK, fall back to vertical month labels:
                estimatedWidth = (labelSize.Height + 0.3f) * numLabels;
                if (estimatedWidth < maximumWidth)
                {
                    // vertical labels will work fine.
                    axisType = AxisType.MonthsVertical;
                    float height = labelSize.Width + tickLength;
                    return height;
                }

                // still too wide - switch to "Q1 '04", "Q2 '04", ect.
                {
                    axisType = AxisType.Quarters;
                    labelSize = g.MeasureString("Q1 '05", f);
                    float height = labelSize.Width + tickLength;
                    return height;
                }
            }
        }

        private delegate DateTime CalculateNext(DateTime v);

        private CalculateNext GetNextMajorFunction()
        {
            // Create a function that, applied to a date/time, returns the
            // "next" value for major labels.
            CalculateNext NextMajor = null;
            if (axisType == AxisType.Quarters)
                NextMajor = delegate(DateTime dt) { return cal.AddMonths(dt, 3); };
            else if (axisType == AxisType.DailyWithHourlyTicks || axisType == AxisType.DailyWithHourlyLabels)
                NextMajor = delegate(DateTime dt) { return cal.AddDays(dt, 1); };
            else
                NextMajor = delegate(DateTime dt) { return cal.AddMonths(dt, 1); };
            return NextMajor;
        }

        private int calculateNumLabels()
        {
            CalculateNext NextMajor = GetNextMajorFunction();
            DateTime v = ScaleMinimum;
            int i;
            for (i = 0; ; i++)
            {
                if (v > ScaleMaximum)
                    break;
                v = NextMajor(v);
            }
            return i + 1;
        }

        public void DrawX(Graphics g, AdvancedRect area, AdvancedRect plotArea)
        {
            drawArea = area;

            //using (Brush br = new SolidBrush(Color.Green))
            //    g.FillRectangle(br, area.Rect);
            GraphicsState _s = g.Save();

            using (Brush br = labelFont.CreateBrush())
            using (Font f = labelFont.CreateFont())
            using (Font f2 = smallLabelFont.CreateFont())
            using (Pen p = tickPen.CreatePen())
            using (Pen pgrid = gridlinePen.CreatePen())
            using (Pen pminor = minorTickPen.CreatePen())
            {
                CalculateNext NextMajor = GetNextMajorFunction();

                // Same dealy-o, but for minor ticks / labels.
                CalculateNext NextMinor = null;
                if (axisType == AxisType.MonthsHorizontalWithDailyTicks || axisType == AxisType.MonthsHorizontalWithDailyLabels)
                    NextMinor = delegate(DateTime dt) { return cal.AddDays(dt, 1); };
                else if (axisType == AxisType.DailyWithHourlyTicks || axisType == AxisType.DailyWithHourlyLabels)
                    NextMinor = delegate(DateTime dt) { return cal.AddHours(dt, 1); };
                else if (axisType == AxisType.Quarters)
                    NextMinor = delegate(DateTime dt) { return cal.AddMonths(dt, 1); };

                // Determine major label format -- {0} is the major label v,
                // {1} is the quarter
                string major_label = null;
                if (axisType == AxisType.Quarters)
                    major_label = "Q{1} {0:\\'yy}";
                else if (axisType == AxisType.DailyWithHourlyTicks || axisType == AxisType.DailyWithHourlyLabels)
                    major_label = "{0:MMM %d \\'yy}";
                else
                    major_label = "{0:MMM \\'yy}";

                // Determine minor label format:
                string minor_label = null;
                if (axisType == AxisType.MonthsHorizontalWithDailyLabels)
                    minor_label = "{0:%d}";
                else if (axisType == AxisType.DailyWithHourlyLabels)
                    minor_label = "{0:%h%t}";

                // Add some extra spacing if putting minor labels in.
                float minorLabelSpacing = 0;
                if (NextMinor != null && minor_label != null)
                    minorLabelSpacing = 1f / 16;

                // StringFormat for drawing major labels:
                StringFormat major_form = new StringFormat();
                if (axisType == AxisType.MonthsVertical || axisType == AxisType.Quarters)
                    major_form.FormatFlags = StringFormatFlags.DirectionVertical;

                // Determine first major label value.
                DateTime v;
                if (axisType == AxisType.Quarters)
                    // set v to beginning of quarter which ScaleMinimum is in
                    v = new DateTime(ScaleMinimum.Year, (((ScaleMinimum.Month - 1) / 3) * 3) + 1, 1);
                else if (axisType == AxisType.DailyWithHourlyTicks || axisType == AxisType.DailyWithHourlyLabels)
                    // set v to beginning of day
                    v = new DateTime(ScaleMinimum.Year, ScaleMinimum.Month, ScaleMinimum.Day);
                else
                    // set v to beginning of month
                    v = new DateTime(ScaleMinimum.Year, ScaleMinimum.Month, 1);

                for (int i = 0; i < calculateNumLabels(); i++)
                {
                    float x1 = DataToCoordinate(v, area);
                    x1 = Math.Max(x1, area.TopLeft.X);

                    if (x1 > area.BottomRight.X)
                        break;

                    DateTime v2 = NextMajor(v);
                    float x2 = DataToCoordinate(v2, area);
                    x2 = Math.Min(x2, area.BottomRight.X);

                    // Major ticks & gridlines:
                    g.DrawLine(p, x1, area.TopLeft.Y, x1, area.TopLeft.Y + tickLength);
                    if (gridlinesEnabled && x1 > area.TopLeft.X && x1 < area.BottomRight.X)
                        g.DrawLine(pgrid, x1, plotArea.TopLeft.Y, x1, plotArea.BottomRight.Y);

                    // Minor ticks:
                    if (NextMinor != null)
                    {
                        DateTime v3 = NextMinor(v);
                        while (v3 < v2)
                        {
                            float xd = DataToCoordinate(v3, area);
                            if (xd > area.TopLeft.X && xd < area.BottomRight.X)
                                g.DrawLine(pminor, xd, area.TopLeft.Y, xd, area.TopLeft.Y + minorTickLength);
                            v3 = NextMinor(v3);
                        }
                    }

                    if (minor_label != null && NextMinor != null)
                    {
                        DateTime v3 = v;
                        while (v3 < v2)
                        {
                            DateTime vnext = NextMinor(v3);

                            float xd_left = DataToCoordinate(v3, area);
                            float xd_right = DataToCoordinate(vnext, area);
                            if (xd_left >= area.TopLeft.X && xd_right <= area.BottomRight.X)
                            {
                                string dtxt = String.Format(minor_label, v3);
                                SizeF dsz = g.MeasureString(dtxt, f2);
                                float xlabel = ((xd_left + xd_right) / 2) - (dsz.Width / 2);
                                if (xlabel > area.TopLeft.X && (xlabel + dsz.Width) < area.BottomRight.X)
                                    g.DrawString(dtxt, f2, br, ((xd_left + xd_right) / 2) - (dsz.Width / 2), area.TopLeft.Y);
                            }

                            v3 = vnext;
                        }
                    }

                    // Draw major label:
                    string txt = String.Format(major_label, v, (v.Month / 3) + 1);
                    SizeF sz = g.MeasureString(txt, f, 100, major_form);
                    float draw_x = ((x1 + x2) / 2) - (sz.Width / 2);
                    if (draw_x > area.TopLeft.X && (draw_x + sz.Width) < area.BottomRight.X)
                        g.DrawString(txt, f, br, ((x1 + x2) / 2) - (sz.Width / 2), area.TopLeft.Y + tickLength + minorLabelSpacing, major_form);

                    v = v2;
                }

                // one final tick to signify end of last visible month
                float xL = DataToCoordinate(v, area);
                if (xL < area.BottomRight.X)
                {
                    g.DrawLine(p, xL, area.TopLeft.Y, xL, area.TopLeft.Y + tickLength);
                    if (gridlinesEnabled)
                        g.DrawLine(pgrid, xL, plotArea.TopLeft.Y, xL, plotArea.BottomRight.Y);
                }
            }

            g.Restore(_s);
        }

        public float DataToCoordinate(DateTime v, AdvancedRect rect)
        {
            return DataToCoordinate(asDouble(v), rect);
        }

        public float DataToCoordinate(double v, AdvancedRect rect)
        {
            double r = (v - asDouble(ScaleMinimum)) / (asDouble(ScaleMaximum) - asDouble(ScaleMinimum));
            return (float)((rect.Width * r) + rect.TopLeft.X);
        }

        public double CoordinateToData(float x, AdvancedRect rect)
        {
            double r = ((x - rect.TopLeft.X) / rect.Width);
            return (asDouble(ScaleMaximum) - asDouble(ScaleMinimum)) * r + asDouble(ScaleMinimum);
        }

        private double asDouble(DateTime v)
        {
            return v.ToOADate();
        }

        private DateTime asDateTime(double d)
        {
            return DateTime.FromOADate(d);
        }

        private double? asNullableDouble(DateTime? v)
        {
            if (v == null)
                return null;
            return asDouble(v.Value);
        }

        private DateTime? asNullableDateTime(double? v)
        {
            if (v == null)
                return null;
            return asDateTime(v.Value);
        }

        public DateTime ScaleMaximum
        {
            get
            {
                if (zoomedMaximum.HasValue)
                    return zoomedMaximum.Value;
                if (userMaximum.HasValue)
                    return userMaximum.Value;
                if (parent.Series.MaxX != null)
                {
                    DateTime maxX = asDateTime(parent.Series.MaxX.Value);
                    DateTime minX = asDateTime(parent.Series.MinX.Value);
                    return maxX + new TimeSpan((maxX - minX).Ticks / 20);
                }
                return unscaledMaximum;
            }
        }

        public DateTime ScaleMinimum
        {
            get
            {
                if (zoomedMinimum.HasValue)
                    return zoomedMinimum.Value;
                if (userMinimum.HasValue)
                    return userMinimum.Value;
                if (parent.Series.MinX != null)
                {
                    DateTime maxX = asDateTime(parent.Series.MaxX.Value);
                    DateTime minX = asDateTime(parent.Series.MinX.Value);
                    return minX - new TimeSpan((maxX - minX).Ticks / 20);
                }
                return unscaledMinimum;
            }
        }

        public DateTime? UserMaximum2
        {
            get
            {
                return userMaximum;
            }
            set
            {
                userMaximum = value;
                raiseEvent();
            }
        }

        public DateTime? UserMinimum2
        {
            get
            {
                return userMinimum;
            }
            set
            {
                userMinimum = value;
                raiseEvent();
            }
        }

        public double? UserMaximum
        {
            get
            {
                return asNullableDouble(userMaximum);
            }
            set
            {
                userMaximum = asNullableDateTime(value);
                raiseEvent();
            }
        }

        public double? UserMinimum
        {
            get
            {
                return asNullableDouble(userMinimum);
            }
            set
            {
                userMinimum = asNullableDateTime(value);
                raiseEvent();
            }
        }

        public double? ZoomedMaximum
        {
            get
            {
                return asNullableDouble(zoomedMaximum);
            }
            set
            {
                zoomedMaximum = asNullableDateTime(value);
                raiseEvent();
            }
        }

        public double? ZoomedMinimum
        {
            get
            {
                return asNullableDouble(zoomedMinimum);
            }
            set
            {
                zoomedMinimum = asNullableDateTime(value);
                raiseEvent();
            }
        }
    }
}
