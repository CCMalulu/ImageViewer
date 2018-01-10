#include "ImageSaver.h"
#include <exception>
#include <fstream>
#include "ImageLoader.h"
#include "stb_loader.h"

bool save_png(const char* filename, int width, int height, int components, const void* data)
{
	try
	{
		stb_save_png(filename, width, height, components, data);
	}
	catch (const std::exception& e)
	{
		set_error(e.what());
		return false;
	}
	return true;
}

bool save_bmp(const char* filename, int width, int height, int components, const void* data)
{
	try
	{
		stb_save_bmp(filename, width, height, components, data);
	}
	catch (const std::exception& e)
	{
		set_error(e.what());
		return false;
	}
	return true;
}

bool save_hdr(const char* filename, int width, int height, int components, const void* data)
{
	try
	{
		stb_save_hdr(filename, width, height, components, data);
	}
	catch (const std::exception& e)
	{
		set_error(e.what());
		return false;
	}
	return true;
}

bool save_pfm(const char* filename, int width, int height, int components, const void* data)
{
	if(components != 1 && components != 3) return false;

	std::ofstream file(filename, std::ios::binary);
	if(!file.bad() && !file.fail())
	{
		file.write(components == 3 ? "PF\n" : "Pf\n", sizeof(char) * 3);
		file << width << " " << height << "\n";

		file.write("-1.000000\n", sizeof(char) * 10);

		const float * v = reinterpret_cast<const float*>(data);
		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				for(int c = 0; c < components; ++c)
				file.write(reinterpret_cast<const char*>(&v[c + components * (x + (height-1-y) * width)]), sizeof(float));
			}
		}
		return true;
	}
	else
	{
		set_error( "Writing hdr image to " + std::string(filename) + ". cannot open file.\n");
		return false;
	}
}
