namespace BizDoc;

internal static class MenuIcons
{
    public static void Home(Graphics graphics, Pen pen)
    {
        graphics.DrawLines(pen, new Point[] { new(2, 11), new(12, 2), new(22, 11) });
        graphics.DrawLines(pen, new Point[] { new(5, 9), new(5, 22), new(10, 22), new(10, 15),
            new(14, 15), new(14, 22), new(19, 22), new(19, 9) });
    }

    public static void Quote(Graphics graphics, Pen pen)
    {
        DrawSheet(graphics, pen);
        graphics.DrawLine(pen, 8, 12, 16, 12);
        graphics.DrawLine(pen, 8, 16, 16, 16);
    }

    public static void Statement(Graphics graphics, Pen pen)
    {
        DrawSheet(graphics, pen);
        graphics.DrawRectangle(pen, 7, 11, 10, 7);
        graphics.DrawLine(pen, 11, 11, 11, 18);
        graphics.DrawLine(pen, 7, 14.5f, 17, 14.5f);
    }

    public static void Documents(Graphics graphics, Pen pen)
    {
        graphics.DrawLines(pen, new Point[] { new(7, 2), new(21, 2), new(21, 18) });
        graphics.DrawRectangle(pen, 3, 6, 14, 16);
        graphics.DrawLine(pen, 6, 11, 14, 11);
        graphics.DrawLine(pen, 6, 15, 14, 15);
        graphics.DrawLine(pen, 6, 19, 11, 19);
    }

    public static void Customers(Graphics graphics, Pen pen)
    {
        graphics.DrawEllipse(pen, 5, 3, 7, 7);
        graphics.DrawArc(pen, 13, 4, 6, 6, -90, 180);
        graphics.DrawArc(pen, 2, 13, 13, 13, 180, 180);
        graphics.DrawArc(pen, 12, 13, 10, 13, -75, 75);
        graphics.DrawLine(pen, 2, 19.5f, 2, 21);
        graphics.DrawLine(pen, 2, 21, 22, 21);
        graphics.DrawLine(pen, 15, 19.5f, 15, 21);
        graphics.DrawLine(pen, 22, 19.5f, 22, 21);
    }

    public static void Product(Graphics graphics, Pen pen)
    {
        graphics.DrawPolygon(pen, new Point[] { new(12, 2), new(21, 7), new(21, 17), new(12, 22), new(3, 17), new(3, 7) });
        graphics.DrawLines(pen, new Point[] { new(3, 7), new(12, 12), new(21, 7) });
        graphics.DrawLine(pen, 12, 12, 12, 22);
    }

    public static void Statistics(Graphics graphics, Pen pen)
    {
        graphics.DrawRectangle(pen, 3, 13, 4, 8);
        graphics.DrawRectangle(pen, 10, 8, 4, 13);
        graphics.DrawRectangle(pen, 17, 3, 4, 18);
    }

    public static void Settings(Graphics graphics, Pen pen)
    {
        const int teeth = 8;
        var points = new PointF[teeth * 4];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = index * MathF.Tau / points.Length;
            var radius = index % 4 is 1 or 2 ? 10 : 7.8f;
            points[index] = new(12 + radius * MathF.Cos(angle), 12 + radius * MathF.Sin(angle));
        }
        graphics.DrawPolygon(pen, points);
        graphics.DrawEllipse(pen, 9, 9, 6, 6);
    }

    private static void DrawSheet(Graphics graphics, Pen pen)
    {
        graphics.DrawPolygon(pen, new Point[] { new(4, 2), new(14, 2), new(20, 8), new(20, 22), new(4, 22) });
        graphics.DrawLines(pen, new Point[] { new(14, 2), new(14, 8), new(20, 8) });
    }
}
