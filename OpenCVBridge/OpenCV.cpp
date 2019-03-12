#include "pch.h"
#include "OpenCV.h"
#include "MemoryBuffer.h"

using namespace OpenCVBridge;

OpenCVLibrary::OpenCVLibrary()
{
}

void OpenCVLibrary::ApplyLipstickPrimaryMethod(SoftwareBitmap ^ input, SoftwareBitmap ^ output, Windows::Foundation::Collections::IVector<Windows::Foundation::Point>^ Points, Color SelectedColors)
{
	if (Points->Size != 20)
	{
		return;
	}

	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}
	cvtColor(inputMat, inputMat, COLOR_BGRA2BGR);

	vector<vector<Point2i>> contour;
	vector<Point2i> LipsPoints;
	for each(Windows::Foundation::Point item in Points)
	{
		LipsPoints.push_back(Point2i(item.X, item.Y));
	}
	contour.push_back(LipsPoints);
	Mat ROI = Mat::zeros(inputMat.size(), CV_8UC3);
	Mat Result;
	try
	{
		drawContours(ROI, contour, 0, Scalar::all(255), -1);

		floodFill(ROI, Point2i(LipsPoints[0].x, LipsPoints[0].y), Scalar(SelectedColors.B, SelectedColors.G, SelectedColors.R));

		addWeighted(inputMat, 0.70, ROI, 0.3, 0, Result);

	}
	catch (cv::Exception) 
	{
		return;
	}

	Result.convertTo(Result, Result.type(), 1.2, 15);

	Result.copyTo(outputMat);

	cvtColor(Result, outputMat, COLOR_BGR2BGRA);
}

bool OpenCVLibrary::GetPointerToPixelData(SoftwareBitmap ^ bitmap, unsigned char ** pPixelData, unsigned int * capacity)
{
	BitmapBuffer^ bmpBuffer = bitmap->LockBuffer(BitmapBufferAccessMode::ReadWrite);
	IMemoryBufferReference^ reference = bmpBuffer->CreateReference();
	ComPtr<IMemoryBufferByteAccess> pBufferByteAccess;
	if ((reinterpret_cast<IInspectable*>(reference)->QueryInterface(IID_PPV_ARGS(&pBufferByteAccess))) != S_OK)
	{
		return false;
	}
	if (pBufferByteAccess->GetBuffer(pPixelData, capacity) != S_OK)
	{
		return false;
	}
	return true;
}

bool OpenCVLibrary::TryConvert(SoftwareBitmap ^ from, Mat & convertedMat)
{
	unsigned char* pPixels = nullptr;
	unsigned int capacity = 0;
	if (!GetPointerToPixelData(from, &pPixels, &capacity))
	{
		return false;
	}
	Mat mat(from->PixelHeight, from->PixelWidth, CV_8UC4, (void*)pPixels);
	convertedMat = mat;
	return true;
}


