using System.Windows;
using System.Windows.Media;
using Mk20Box.Ui;
using NUnit.Framework;

namespace Mk20Box.Tests.Ui
{
    /// <summary>
    /// Covers the crop maths the editor previews with. It has to agree with what
    /// the theme composer will actually produce, or the preview lies.
    /// </summary>
    [TestFixture]
    public class CropPreviewTests
    {
        private static CropPreview Square()
        {
            return new CropPreview(128, 128);
        }

        [Test]
        public void StretchFor_PadsWhenFittingAndFillsOtherwise()
        {
            CropPreview crop = Square();

            Assert.Multiple(() =>
            {
                Assert.That(crop.StretchFor(true), Is.EqualTo(Stretch.Uniform));
                Assert.That(crop.StretchFor(false), Is.EqualTo(Stretch.Fill));
            });
        }

        [Test]
        public void Viewbox_ShowsEverythingWhenFitting()
        {
            Rect box = Square().Viewbox(@"C:\nope\missing.png", true, 0, 0);

            Assert.That(box, Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [Test]
        public void Viewbox_ShowsEverythingWhenTheSizeIsUnknown()
        {
            // A missing file has no aspect to crop against, so nothing is hidden.
            Rect box = Square().Viewbox(@"C:\nope\missing.png", false, 0, 0);

            Assert.That(box, Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Viewbox_ShowsEverythingWithoutAPath(string path)
        {
            Assert.That(Square().Viewbox(path, false, 0, 0), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [Test]
        public void CanPan_IsFalseWhenNothingIsHidden()
        {
            CropPreview crop = Square();

            Assert.Multiple(() =>
            {
                Assert.That(crop.CanPanX(@"C:\nope\missing.png", true), Is.False);
                Assert.That(crop.CanPanY(@"C:\nope\missing.png", true), Is.False);
            });
        }

        [Test]
        public void Pan_MovesByTwiceTheFraction()
        {
            // The offset runs -1..1 across the image, so half a screen is a whole 1.
            Assert.That(CropPreview.Pan(0, 0.25), Is.EqualTo(0.5).Within(0.0001));
        }

        [Test]
        public void Pan_AccumulatesAcrossDrags()
        {
            double offset = CropPreview.Pan(0, 0.1);
            offset = CropPreview.Pan(offset, 0.1);

            Assert.That(offset, Is.EqualTo(0.4).Within(0.0001));
        }

        [TestCase(0.9, 1.0, 1.0)]
        [TestCase(-0.9, -1.0, -1.0)]
        [TestCase(0.0, 5.0, 1.0)]
        [TestCase(0.0, -5.0, -1.0)]
        public void Pan_StopsAtTheEdges(double offset, double fraction, double expected)
        {
            Assert.That(CropPreview.Pan(offset, fraction), Is.EqualTo(expected).Within(0.0001));
        }

        [Test]
        public void Invalidate_CanBeCalledSafely()
        {
            CropPreview crop = Square();

            crop.Invalidate();

            Assert.That(crop.Viewbox(null, false, 0, 0), Is.EqualTo(new Rect(0, 0, 1, 1)));
        }
    }
}
