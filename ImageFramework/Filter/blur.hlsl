#setting sepa, true
#setting title, Gaussian Blur
#setting description, The visual effect of this blurring technique is a smooth blur resembling that of viewing the image through a translucent screen, distinctly different from the bokeh effect produced by an out-of-focus lens or the shadow of an object under usual illumination
#setting type, dynamic

#param Blur Radius, blur_radius, int, 20, 1
#param Variance, variance, float, 72.46, 1
#param Blur Alpha, blur_alpha, bool, false

// Simple Gauss-Kernel. Normalization is not included and must be
// done by dividing through the weight sum.
float kernel(int _offset)
{
	return exp(- _offset * _offset / variance);
}

float4 getPixel(int3 pos, int3 size)
{
	pos = clamp(pos, 0, size-1);
	return src_image[texel(pos)];
}

float4 filter(int3 pixelCoord, int3 size)
{
	float4 pixelSum = 0.0;
	float weightSum = 0.0;
	float alpha = src_image[texel(pixelCoord)].a;
	
	for(int d = -blur_radius; d <= blur_radius; d++)
	{			
		float w = kernel(d);
		weightSum += w;
		int3 pos = d * filterDirection + pixelCoord;
		pixelSum += w * getPixel(pos, size);
	}
	
	if(blur_alpha)
		alpha = pixelSum.a;

	return float4(pixelSum.rgb / weightSum, alpha);
}