using DlibDotNet;
using OpenCVBridge;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Buffer = Windows.Storage.Streams.Buffer;

namespace CosmeticsEffect
{
    public sealed class CosmeticsVideoEffect : IBasicVideoEffect
    {
        private VideoEncodingProperties EncodingProperties;
        private IPropertySet Configuration;
        private List<Windows.Foundation.Point> PointsCollection;
        private byte[] DlibImageArray;
        private FrontalFaceDetector FaceDetector;
        private ShapePredictor FaceModel;
        private Color LipColor
        {
            get
            {
                if (Configuration.TryGetValue("LipColor", out object Val))
                {
                    return (Color)Val;
                }
                else
                {
                    return default;
                }
            }
        }
        private uint BufferSize;

        public CosmeticsVideoEffect()
        {
            if (ApplicationData.Current.LocalSettings.Values["ReturnCosmeticsEffectExcution"] is false)
            {
                PointsCollection = new List<Windows.Foundation.Point>();
                FaceDetector = Dlib.GetFrontalFaceDetector();
                FaceModel = ShapePredictor.Deserialize(Package.Current.InstalledLocation.Path + "/Cosmetics/shape_predictor_68_face_landmarks.dat");
            }
        }

        public void Close(MediaEffectClosedReason reason)
        {
            if (ApplicationData.Current.LocalSettings.Values["ReturnCosmeticsEffectExcution"] is false)
            {
                FaceDetector?.Dispose();
                FaceModel?.Dispose();
                FaceDetector = null;
                FaceModel = null;
                EncodingProperties = null;
                Configuration = null;
                PointsCollection?.Clear();
                PointsCollection = null;
                DlibImageArray = null;
            }
        }

        public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            EncodingProperties = encodingProperties;
            BufferSize = EncodingProperties.Height * EncodingProperties.Width * 4;
            DlibImageArray = new byte[BufferSize];
        }

        public void ProcessFrame(ProcessVideoFrameContext context)
        {
            if (context.InputFrame.SoftwareBitmap != null)
            {
                using (SoftwareBitmap IncomeSoftwareBitmap = SoftwareBitmap.Copy(context.InputFrame.SoftwareBitmap))
                {
                    //将图像转换成byte数组
                    Buffer buffer = new Buffer(BufferSize);
                    IncomeSoftwareBitmap.CopyToBuffer(buffer);
                    using (var Reader = DataReader.FromBuffer(buffer))
                    {
                        Reader.ReadBytes(DlibImageArray);
                    }

                    //将byte数组转换为Dlib可识别的数据
                    using (Array2D<RgbPixel> ImageData = Dlib.LoadImageData<RgbPixel>(ImagePixelFormat.Bgra, DlibImageArray, EncodingProperties.Height, EncodingProperties.Width, EncodingProperties.Width * 4))
                    {

                        //检测人脸并将嘴唇特征点提取并打包成点集
                        IEnumerable<FullObjectDetection> Faces = DlibFunction(ImageData);
                        if (Faces != null)
                        {
                            for (int j = 0; j < Faces.Count(); j++)
                            {
                                using (FullObjectDetection FaceObject = Faces.ElementAt(j))
                                {
                                    for (uint i = 48; i < FaceObject.Parts; i++)
                                    {
                                        var Points = FaceObject.GetPart(i);
                                        PointsCollection.Add(new Windows.Foundation.Point(Points.X, Points.Y));
                                    }
                                }

                                //调用OpenCVBridge提供的API，进行OpenCV处理

                                OpenCVLibrary.ApplyLipstickPrimaryMethod(IncomeSoftwareBitmap, IncomeSoftwareBitmap, PointsCollection, LipColor);
                                PointsCollection.Clear();
                            }
                            IncomeSoftwareBitmap.CopyTo(context.OutputFrame.SoftwareBitmap);
                        }
                        else
                        {
                            IncomeSoftwareBitmap.CopyTo(context.OutputFrame.SoftwareBitmap);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<FullObjectDetection> DlibFunction(Array2D<RgbPixel> img)
        {
            Rectangle[] dets = FaceDetector.Operator(img);
            IEnumerable<FullObjectDetection> FaceLandMarkContainer = from rect in dets
                                                                     let FaceLandMarkRawData = FaceModel.Detect(img, rect)
                                                                     where FaceLandMarkRawData.Parts > 2
                                                                     select FaceLandMarkRawData;
            return FaceLandMarkContainer.Any() ? FaceLandMarkContainer : null;
        }

        public void DiscardQueuedFrames()
        {

        }

        public bool IsReadOnly => false;

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties
        {
            get
            {
                return new List<VideoEncodingProperties>()
                {
                    new VideoEncodingProperties
                    {
                        Subtype="ARGB32"
                    }
                };
            }
        }

        public MediaMemoryTypes SupportedMemoryTypes => MediaMemoryTypes.Cpu;

        public bool TimeIndependent => true;

        public void SetProperties(IPropertySet configuration)
        {
            Configuration = configuration;
        }
    }
}
