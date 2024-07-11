# AnythingToGif

This is a versatile tool designed to convert a wide variety of visual media formats into high-quality GIFs (with a hard 'G'), supporting TrueColor images. This utility excels in converting both still images and video files into GIFs, ensuring superior color fidelity and efficient processing.

- [X] Command line interface

```
AnythingToGif 1.0.2.0
Copyright (C) 2024 Hawkynt
Converts anything to GIF format.

Usage: AnythingToGif [<input>] [<options>] | <input> <output> [<options>]

  -q, --quantizer                       (Default: Octree) Quantizer to use.
  -d, --ditherer                        (Default: FloydSteinberg) Ditherer to use.
  -f, --useBackFilling                  (Default: false) Whether to use backfilling.
  -b, --firstSubImageInitsBackground    (Default: true) Whether the first sub-image initializes the background.
  -c, --colorOrdering                   (Default: MostUsedFirst) Color ordering mode.
  -n, --noCompression                   (Default: false) Whether to use compressed GIF files or not.
  --help                                Display this help screen.
  --version                             Display version information.
  input (pos. 0)                        Input directory or file. If not specified, defaults to the current directory.
  output (pos. 1)                       Output directory or file. If not specified, defaults to the current directory.

Quantizer Modes:
  MedianCut: Median-Cut
  Octree: Octree
  GreedyOrthogonalBiPartitioning: Greedy Orthogonal Bi-Partitioning (Wu)

Ditherer Modes:
  None: None
  FloydSteinberg: Floyd-Steinberg
  JarvisJudiceNinke: Jarvis-Judice-Ninke
  Stucki: Stucki
  Atkinson: Atkinson
  Burkes: Burkes
  Sierra: Sierra
  TwoRowSierra: 2-row Sierra
  SierraLite: Sierra Lite
  Pigeon: Pigeon

Color Ordering Modes:
  MostUsedFirst: Ordered by usage, the most used first
  FromCenter:
  LeastUsedFirst: Ordered by usage, the least used first
  HighLuminanceFirst: Ordered by luminance, the brightest colors first
  LowLuminanceFirst: Ordered by luminance, the darkest colors first
  Random: Purely random

Insufficient arguments try '--help' for help.
```

## Overview

In the 1990s, the GIF file format was the dominant image format on the web, known for its efficiency, portability, and support for animation and transparency. However, due to concerns over patent claims on the LZW compression algorithm, the PNG format was introduced as a replacement, offering several advantages over GIF. Despite these advantages, it was often believed that GIFs were limited to 256-color palettes, making them unsuitable for full-color images. This belief is only partially correct. GIFs can indeed contain many colors by utilizing multiple graphic rendering blocks, each with its own local color table.

### High-Color GIFs Explained

GIF files traditionally support an 8-bit pixel format, meaning each pixel value references a table with up to 256 colors. However, this limitation can be circumvented by using multiple graphic rendering blocks within a single GIF image. Each block can have its own local color table of 256 colors, drawn from a 24-bit color space.

To create a high-color GIF, **AnythingToGif** partitions the color set across multiple frames. The process works as follows:

1. **First Frame**: The initial frame contains 256 commonly used colors from the original image, using traditional quantization and dithering techniques to ensure a high-quality base image. This approach ensures that the initial frame is a visually appealing approximation of the final image, allowing the image to visually converge faster. By starting with a high-quality base, the subsequent frames enhance the image more efficiently, leading to a smoother and quicker rendering process. This method improves both the visual quality and the user experience, as the GIF appears more complete and detailed from the beginning. The user can also start with a blank canvas if he choses to do so. This is controlled by this *FirstSubImageInitsBackground* flag.

2. **Subsequent Frames**: Each subsequent frame introduces 255 new colors. The 256th color in each frame is reserved for transparency, allowing the frame to be overlaid on the previous ones without completely obscuring them. This utilizes a feature of the GIF format called local color table which is bound to a single frame.

   *Color Ordering Method*: The colors for each frame are added based on a selected color ordering method, which determines the sequence in which new colors are introduced. This method ensures that the most visually significant colors are prioritized.

   The options include:

   - [X] **Random**: Colors are added in a random order.
   - [X] **MostUsedFirst**: Colors that appear most frequently in the image are added first, ensuring that the most common colors are prioritized.
   - [ ] **FromCenter**: Colors are added starting from the center of the image, moving outward.
   - [X] **LeastUsedFirst**: Colors that appear least frequently are added first.
   - [X] **HighLuminanceFirst**: Colors with the highest brightness levels are added first, enhancing bright areas of the image initially.
   - [X] **LowLuminanceFirst**: Colors with the lowest brightness levels are added first, focusing on darker areas initially.

3. **Layering Frames**: The frames are layered on top of each other using a GIF feature called the *frame disposal method* combined with the lowest possible *frame delay* of 1/100th second. This method ensures that already existing pixels remain on the screen, untouched by subsequent frames. When combined with transparency, it allows new colors to be added without overwriting previous ones. Only the parts of the image that need new colors are repainted in each frame, making the layering process efficient and preserving the visual integrity of the image as it builds up to the full-color representation.

   - [ ] **Dynamic frame shift**: Only save the changed parts by utilizing frame offsets and arbitrary sized frames.

4. **Incremental Improvement**: This layering approach enables the GIF to incrementally improve the color representation of the image. Initially, the image may appear coarse because the first frame contains only the 256 commonly used colors. However, as more frames are rendered, each introducing new colors, the image quality progressively converges closer to the original full-color image.

   The process can be customized using the *UseBackFilling* flag. When the *UseBackFilling* flag is enabled, the tool not only paints pixels with exact color matches from the current palette but also approximates other areas with the nearest available colors. This means that even areas without an exact color match are painted in each increment, providing a more visually complete image early on. These areas are repainted in subsequent frames as more colors become available, resulting in a smoother and faster visual convergence.

   Alternatively, if the *UseBackFilling* flag is disabled, the tool will only paint pixels that have exact color matches in the current palette. This method ensures that each pixel is only painted when its exact color is available, which might result in a more staggered convergence but maintains precise color accuracy throughout the process.

Further Links for this part:

- [TrueColorGIF Application](https://github.com/donatj/tcgif)
- [Technical Description](http://notes.tweakblogs.net/blog/8712/high-color-gif-images.html)

### Color Quantization

The initial frame requires an approximate palette that represents the full range of colors in the image. Several methods for color quantization are employed to achieve this, including:

- [x] [Median-cut (MC)](https://gowtham000.hashnode.dev/median-cut-a-popular-colour-quantization-strategy)
- [x] [Octree (OC)](https://www.codeproject.com/Articles/109133/Octree-Color-Palette)
- [ ] [Variance-based method (WAN)](http://algorithmicbotany.org/papers/variance-based.pdf)
- [ ] [Binary splitting (BS)](https://opg.optica.org/josaa/fulltext.cfm?uri=josaa-11-11-2777&id=847)
- [x] Greedy orthogonal bi-partitioning method (WU)
- [ ] [Neuquant (NQ)](https://scientificgems.wordpress.com/stuff/neuquant-fast-high-quality-image-quantization/)
- [ ] [Adaptive distributing units (ADU)](https://www.tandfonline.com/doi/full/10.1179/1743131X13Y.0000000059?needAccess=true)
- [ ] [Variance-cut (VC)](https://ieeexplore.ieee.org/document/6718239)
- [ ] [WU combined with Ant-tree for color quantization (ATCQ or WUATCQ)](https://github.com/mattdesl/atcq)
- [ ] [BS combined with iterative ATCQ (BSITATCQ)](https://www.mdpi.com/2076-3417/10/21/7819)

Further Links for this part:

- [Quantizers](https://www.codeproject.com/Articles/66341/A-Simple-Yet-Quite-Powerful-Palette-Quantizer-in-C)

### Dithering

Dithering techniques are applied to ensure the first frame provides a good base image. Methods include:

- [X] None
- [X] [Floyd-Steinberg](https://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering)
- [X] [Floyd-Steinberg (equally distributed)](https://github.com/kgjenkins/dither-dream)
- [X] [Jarvis, Judice, and Ninke](https://www.graphicsacademy.com/what_ditherjarvis.php) [[1](https://www.researchgate.net/figure/Difference-between-Jarvis-Judice-and-Ninke-and-Floyd-Steinberg-results-from-watch-input_fig3_342085636)]
- [X] Stucki
- [X] Atkinson
- [X] Burkes
- [X] Sierra
- [X] Two-Row Sierra
- [X] Sierra Lite
- [X] [Pigeon](https://hbfs.wordpress.com/2013/12/31/dithering/)
- [ ] [Riemersma](https://www.compuphase.com/riemer.htm) [[1](https://github.com/ibezkrovnyi/image-quantization/blob/main/packages/image-q/src/image/riemersma.ts)]
- [ ] [Bayer-Matrix](https://github.com/dmnsgn/bayer)
- [ ] [Average](https://www.graphicsacademy.com/what_dithera.php)
- [ ] [Random](https://www.graphicsacademy.com/what_ditherr.php)

Further Links for this part:

- [Dithering Matrices](https://tannerhelland.com/2012/12/28/dithering-eleven-algorithms-source-code.html)
- [DitherPunk](https://surma.dev/things/ditherpunk/)
- [Cyotek](https://github.com/cyotek/Dithering/tree/master/src/Dithering)

### Color distance calculation

- [X]  [Weighted Euclidean](https://www.compuphase.com/cmetric.htm)

Further Links for this part:

- [Wikipedia](https://en.wikipedia.org/wiki/Color_difference)

### GIF Format

Due to the nature of this application, **AnythingToGif** requires fine-grained control over the bytes written to disk. This includes managing local palettes, transparency, frame delays, and frame disposal methods. To achieve this level of precision, the tool incorporates its own GIF writing code, built directly from the GIF specifications. This custom code ensures that every aspect of the GIF format is meticulously handled, allowing for the creation of high-color images and smooth animations. The output is rigorously checked using various GIF debugging tools, ensuring compatibility and optimal performance across different platforms and browsers.

- [X] [Uncompressed Images](https://github.com/Distrotech/libungif/blob/master/UNCOMPRESSED_GIF)
- [X] [LZW Compression](https://giflib.sourceforge.net/whatsinagif/lzw_image_data.html)
- [ ] [Optimized compression](https://create.stephan-brumme.com/flexigif-lossless-gif-lzw-optimization/)

Further Links for this part:

- [GIF Specs](https://www.w3.org/Graphics/GIF/spec-gif89a.txt)
- [GIF Format](https://giflib.sourceforge.net/whatsinagif/)
- [Online GIF Debugger](https://onlinegiftools.com/analyze-gif)
- [GIF Checker](https://interglacial.com/pub/dr_gif_80g.pl)
- [GIF Explorer](https://www.matthewflickinger.com/lab/whatsinagif/gif_explorer.asp)
- [Palette Paper](https://iplab.dmi.unict.it/iplab/wp-content/uploads/2023/09/Animated_Gif_Optimization_By_Adaptive_Color_Local_Table_Management-1.pdf)

## Practical Considerations

While high-color GIFs can accurately represent complex images, they often result in large file sizes due to the numerous frames required. One approach to mitigate this is to encode more image information into the first few frames, creating an approximation of the full image and refining it in subsequent frames. This results in larger files because fewer pixels are transparent, but it improves the visual quality of the initial rendering.

Reducing the number of distinct colors in the image can also help manage file size and loading times. For example, using just 5 frames allows for (5 * 255) + 1 = 1276 (2551 for 10 frames ~0.1sec, 5101 for 20 frames ~0.2sec, 2.550.001 for 100 frames ~1sec) different colors, which is a significant improvement over the traditional 256-color palette.

## Converting video

When converting video, AnythingToGif processes changed areas from frame to frame, introducing new frames as necessary using the same algorithms applied to still images. This ensures high-quality color reproduction and smooth transitions at the cost of a higher framerate whenever needed.

- [X] **Frame extrapolation**: Get the images from a video.
- [ ] **Differential frame encoding**: Only process the differencies between each frame.
- [ ] **Constant FPS**: Switching between constant fps inserting dummy frames as needed or variable frame rate
- [ ] **Concatenation**: Combining multiple files into one video.