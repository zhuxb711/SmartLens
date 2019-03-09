#pragma once
#include <opencv2\core\core.hpp>
#include <opencv2\imgproc\imgproc.hpp>
#include <opencv2\video.hpp>
#include <vector>
using namespace Windows::Graphics::Imaging;
using namespace Windows::UI;
using namespace std;
using namespace cv;
using namespace Platform;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;

namespace OpenCVBridge
{
	public ref class OpenCVLibrary sealed
    {
    public:
        OpenCVLibrary();

		void ApplyLipstickPrimaryMethod(SoftwareBitmap^ input, SoftwareBitmap^ output, Windows::Foundation::Collections::IVector<Windows::Foundation::Point>^ Points, Color SelectedColors);

	private:

		bool GetPointerToPixelData(SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity);

		bool TryConvert(SoftwareBitmap^ from, Mat& convertedMat);
    };
}
