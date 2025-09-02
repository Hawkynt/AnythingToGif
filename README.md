# AnythingToGif

[![License](https://img.shields.io/badge/License-LGPL_3.0-blue)](https://licenses.nuget.org/LGPL-3.0-or-later)
![Language](https://img.shields.io/github/languages/top/Hawkynt/AnythingToGif?color=purple)

> This is a versatile tool designed to convert a wide variety of visual media formats into high-quality GIFs (with a hard 'G'), supporting TrueColor images. This utility excels in converting both still images and video files into GIFs, ensuring superior color fidelity and efficient processing.

- [X] Command line interface

```
AnythingToGif 1.0.2.0
Copyright (C) 2025 Hawkynt
Converts anything to GIF format.

Usage: AnythingToGif [<input>] [<options>] | <input> <output> [<options>]

  -a, --useAntRefinement                (Default: false) Whether to apply Ant-tree like iterative refinement after
                                        initial quantization.
  -b, --firstSubImageInitsBackground    (Default: true) Whether the first sub-image initializes the background.
  -c, --colorOrdering                   (Default: MostUsedFirst) Color ordering mode.
  -d, --ditherer                        (Default: FloydSteinberg) Ditherer to use.
  --bayer                               (Default: 0) Generate 2^n Bayer matrix (e.g., --bayer 4 creates 16x16 matrix).
                                        When specified, overrides --ditherer. Valid range: 1-8.
  -f, --useBackFilling                  (Default: false) Whether to use backfilling.
  -i, --antIterations                   (Default: 25) Number of iterations for Ant-tree like refinement.
  -m, --metric                          (Default: Default) Color distance metric to use.
  -n, --noCompression                   (Default: false) Whether to use compressed GIF files or not.
  -p, --usePca                          (Default: false) Use PCA (Principal Component Analysis) preprocessing before
                                        quantization.
  -q, --quantizer                       (Default: Octree) Quantizer to use.
  --help                                Display this help screen.
  --version                             Display version information.
  input (pos. 0)                        Input directory or file. If not specified, defaults to the current directory.
  output (pos. 1)                       Output directory or file. If not specified, defaults to the current directory.

Color Distance Metrics:
  Default: Let application decide
  Euclidean: Euclidean
  EuclideanRGBOnly: Euclidean (RGB only)
  Manhattan: Manhattan
  ManhattanRGBOnly: Manhattan (RGB only)
  CompuPhase: CompuPhase
  EuclideanBT709: Weighted Euclidean (BT.709)
  EuclideanNommyde: Weighted Euclidean (Nommyde)
  WeightedEuclideanLowRed: Weighted Euclidean (low red component)
  WeightedEuclideanHighRed: Weighted Euclidean (high red component)
  ManhattanBT709: Weighted Manhattan (BT.709)
  ManhattanNommyde: Weighted Manhattan (Nommyde)
  WeightedManhattanLowRed: Weighted Manhattan (low red component)
  WeightedManhattanHighRed: Weighted Manhattan (high red component)
  PNGQuant: PNGQuant
  WeightedYuv: Weighted YUV
  WeightedYCbCr: Weighted YCbCr
  CieDe2000: CIEDE2000
  Cie94Textiles: CIE94 Textiles
  Cie94GraphicArts: CIE94 Graphic Arts

Quantizer Modes:
  Ega16: EGA 16-colors
  Vga256: VGA 256-colors
  WebSafe: Web Safe palette
  Mac8Bit: Mac 8-bit system palette
  MedianCut: Median-Cut
  Octree: Octree
  GreedyOrthogonalBiPartitioning: Greedy Orthogonal Bi-Partitioning (Wu)
  VarianceCut: Variance-Cut
  VarianceBased: Variance-Based
  BinarySplitting: Binary Splitting
  Adu: Adaptive Distributing Units

Ditherer Modes:
  None: None
  FloydSteinberg: Floyd-Steinberg
  EqualFloydSteinberg: Equally-Distributed Floyd-Steinberg
  FalseFloydSteinberg: False Floyd-Steinberg
  JarvisJudiceNinke: Jarvis-Judice-Ninke
  Stucki: Stucki
  Atkinson: Atkinson
  Burkes: Burkes
  Sierra: Sierra
  TwoRowSierra: 2-row Sierra
  SierraLite: Sierra Lite
  Pigeon: Pigeon
  StevensonArce: Stevenson-Arce
  ShiauFan: ShiauFan
  ShiauFan2: ShiauFan2
  Fan93: Fan93
  Bayer2x2: Bayer 2x2
  Bayer4x4: Bayer 4x4
  Bayer8x8: Bayer 8x8
  Bayer16x16: Bayer 16x16
  Halftone8x8: Halftone 8x8
  ADitherXorY149: A-Dither XOR-Y149
  ADitherXorY149WithChannel: A-Dither XOR-Y149 with Channel
  ADitherXYArithmetic: A-Dither XY Arithmetic
  ADitherXYArithmeticWithChannel: A-Dither XY Arithmetic with Channel
  ADitherUniform: A-Dither Uniform
  RiemersmaDefault: Riemersma (Default)
  RiemersmaSmall: Riemersma (Small)
  RiemersmaLarge: Riemersma (Large)
  RiemersmaLinear: Riemersma (Linear)
  WhiteNoise: White Noise (50%)
  WhiteNoiseLight: White Noise (30%)
  WhiteNoiseStrong: White Noise (70%)
  BlueNoise: Blue Noise (50%)
  BlueNoiseLight: Blue Noise (30%)
  BlueNoiseStrong: Blue Noise (70%)
  BrownNoise: Brown Noise (50%)
  BrownNoiseLight: Brown Noise (30%)
  BrownNoiseStrong: Brown Noise (70%)
  PinkNoise: Pink Noise (50%)
  PinkNoiseLight: Pink Noise (30%)
  PinkNoiseStrong: Pink Noise (70%)
  KnollDefault: Knoll (Default)
  KnollBayer8x8: Knoll (8x8 Bayer)
  KnollHighQuality: Knoll (High Quality)
  KnollFast: Knoll (Fast)
  NClosestDefault: N-Closest (Default)
  NClosestWeightedRandom5: N-Closest (Weighted Random 5)
  NClosestRoundRobin4: N-Closest (Round Robin 4)
  NClosestLuminance6: N-Closest (Luminance 6)
  NClosestBlueNoise4: N-Closest (Blue Noise 4)
  NConvexDefault: N-Convex (Default)
  NConvexProjection6: N-Convex (Projection 6)
  NConvexSpatialPattern3: N-Convex (Spatial Pattern 3)
  NConvexWeightedRandom5: N-Convex (Weighted Random 5)
  AdaptiveQualityOptimized: Adaptive (Quality Optimized)
  AdaptiveBalanced: Adaptive (Balanced)
  AdaptivePerformanceOptimized: Adaptive (Performance Optimized)
  AdaptiveSmartSelection: Adaptive (Smart Selection)

Color Ordering Modes:
  MostUsedFirst: Ordered by usage, the most used first
  FromCenter: Ordered by distance to center, most close first
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
   - [X] **FromCenter**: Colors are added starting from the center of the image, moving outward.
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
- [X] [Variance-based method (WAN)](http://algorithmicbotany.org/papers/variance-based.pdf)
- [X] [Binary splitting (BS)](https://opg.optica.org/josaa/fulltext.cfm?uri=josaa-11-11-2777&id=847)
- [X] [Binary splitting with Ant-tree (BSAT)](https://link.springer.com/article/10.1007/s11554-018-0814-8)
- [x] Greedy orthogonal bi-partitioning method (WU)
- [ ] [Neuquant (NQ)](https://scientificgems.wordpress.com/stuff/neuquant-fast-high-quality-image-quantization/)
- [X] [Adaptive distributing units (ADU)](https://www.tandfonline.com/doi/full/10.1179/1743131X13Y.0000000059?needAccess=true)
- [X] [Variance-cut (VC)](https://ieeexplore.ieee.org/document/6718239)
- [X] [WU combined with Ant-tree for color quantization (ATCQ or WUATCQ)](https://github.com/mattdesl/atcq)
- [X] [BS combined with iterative ATCQ (BSITATCQ)](https://www.mdpi.com/2076-3417/10/21/7819)
- [ ] Simulated Annealing
- [X] Fixed Palettes
- [ ] BSDS300
- [x] [Principal Component Analysis]()
- [ ] K-Means

Further Links for this part:

- [Quantizers](https://www.codeproject.com/Articles/66341/A-Simple-Yet-Quite-Powerful-Palette-Quantizer-in-C)

### Dithering

Dithering techniques are applied to ensure the first frame provides a good base image. Methods include:

- [X] None
- ErrorDiffusion
  - [X] [Floyd-Steinberg](https://en.wikipedia.org/wiki/Floyd%E2%80%93Steinberg_dithering)
  - [X] [False Floyd-Steinberg](https://github.com/makew0rld)
  - [X] [Floyd-Steinberg (equally distributed)](https://github.com/kgjenkins/dither-dream)
  - [X] [Jarvis, Judice, and Ninke](https://www.graphicsacademy.com/what_ditherjarvis.php) [[1](https://www.researchgate.net/figure/Difference-between-Jarvis-Judice-and-Ninke-and-Floyd-Steinberg-results-from-watch-input_fig3_342085636)]
  - [X] Stucki
  - [X] Atkinson
  - [X] Burkes
  - [X] Sierra
  - [X] Two-Row Sierra
  - [X] Sierra Lite
  - [X] [Pigeon](https://hbfs.wordpress.com/2013/12/31/dithering/)
  - [X] [Stevenson-Arce](https://github.com/hbldh/hitherdither)
  - [X] [Fan](https://ditherit.com)
  - [X] [ShiauFan](https://ditherit.com)
  - [X] [ShiauFan2](https://ditherit.com)
- Matrix-based
  - [X] [Bayer Matrix](https://github.com/dmnsgn/bayer) (2x2, 4x4, 8x8, 16x16, arbitrary 2^n sizes via CLI)
  - [X] [Halftone](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/)
  - [ ] [Interleaved Gradient Noise]
- [Arithmetic Dither](https://pippin.gimp.org/a_dither/) - Procedural spatial dithering with multiple patterns
  - [X] XOR-Y149 pattern
  - [X] XOR-Y149 with channel variation
  - [X] XY Arithmetic pattern  
  - [X] XY Arithmetic with channel variation
  - [X] Uniform pattern
- [X] [Riemersma](https://www.compuphase.com/riemer.htm) [[1](https://github.com/ibezkrovnyi/image-quantization/blob/main/packages/image-q/src/image/riemersma.ts)] - Space-filling curve based dithering with four variants (Default, Small, Large, Linear)
- Noise-Based Dithering - Statistical noise patterns for dithering:
  - [X] **White Noise**: Uniform random distribution across all frequencies, completely uncorrelated
  - [X] **Blue Noise**: High-frequency emphasis with good spatial distribution, avoids clustering artifacts
  - [X] **Brown Noise**: Low-frequency emphasis with Brownian motion characteristics, smoother patterns
  - [X] **Pink Noise**: 1/f noise with balanced frequency distribution between white and brown noise
  - Each type available in Light (30%), Normal (50%), and Strong (70%) intensity variants
- [ ] [Average](https://www.graphicsacademy.com/what_dithera.php)
- [ ] [Random](https://www.graphicsacademy.com/what_ditherr.php)
- [ ] [Joel Yliluoma's algorithm 1](https://bisqwit.iki.fi/story/howto/dither/jy/)
- [ ] [Joel Yliluoma's algorithm 2](https://bisqwit.iki.fi/story/howto/dither/jy/)
- [ ] [Joel Yliluoma's algorithm 3](https://bisqwit.iki.fi/story/howto/dither/jy/)
- [X] [Thomas Knoll](https://bisqwit.iki.fi/story/howto/dither/jy/) - Advanced ordered dithering with candidate generation (4 variants: Default, Bayer8x8, High Quality, Fast)
- [X] [N-Closest](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/) - Selects from N closest palette colors with multiple strategies (Random, Weighted Random, Round Robin, Luminance, Blue Noise)
- [X] [N-Convex](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/) - Creates convex hull from N closest colors for better color mixing (Barycentric, Projection, Spatial Pattern, Weighted Random)
- [X] **Adaptive Dithering** - Intelligent algorithm selection based on image analysis:
  - **Quality Optimized**: Prioritizes visual quality over performance
  - **Balanced**: Balances quality and performance considerations
  - **Performance Optimized**: Optimizes for speed while maintaining acceptable quality
  - **Smart Selection**: Uses ML-like scoring to select optimal algorithm based on image characteristics (color complexity, edge density, gradient smoothness, noise level, detail level)
- [X] [Ostromoukhov Variable-Coefficient Error Diffusion](https://observablehq.com/@jobleonard/variable-coefficient-dithering) - Advanced error diffusion that varies the dithering kernel based on current pixel value to achieve blue-noise characteristics
- [X] [Yliluoma Ordered Dithering](https://bisqwit.iki.fi/story/howto/dither/jy/) - Joel Yliluoma's arbitrary-palette positional dithering algorithms (1, 2, 3) optimized for better contrast and color fidelity
- [X] **Serpentine/Boustrophedon Scanning** - Use `--serpentine` flag with any matrix-based error diffusion ditherer to apply alternating scan direction per row, eliminating directional artifacts. Works with all MatrixBasedDitherer variants including Floyd-Steinberg, Stucki, JJN, Atkinson, Burkes, Sierra, and others.
- [X] **Structure-Aware Error Diffusion** - Modern error diffusion that preserves image structure and details using circular error distribution:
  - **Default**: Standard structure-aware with 2-pixel radius
  - **Priority**: Priority-based ordering with 3-pixel radius
  - **Large**: Extended radius (4 pixels) for maximum quality
- [X] [Dizzy Dithering](https://liamappelbe.medium.com/dizzy-dithering-2ae76dbceba1) - Novel 2024 error diffusion algorithm that produces blue noise characteristics through spiral pattern distribution:
  - **Default**: Standard dizzy dithering with balanced settings
  - **High Quality**: Reduced randomness for maximum quality
  - **Fast**: Optimized for speed with smaller spiral radius
- [ ] [Barycentric](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/)
- [ ] [Triangulated Irregular Network](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/)
- [ ] [Natural Neighbour](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/)

Further Links for this part:

- [Dithering Matrices](https://tannerhelland.com/2012/12/28/dithering-eleven-algorithms-source-code.html)
- [DitherPunk](https://surma.dev/things/ditherpunk/)
- [Cyotek](https://github.com/cyotek/Dithering/tree/master/src/Dithering)
- [Tetrapal](https://github.com/matejlou/tetrapal)
- [Dithermark](https://dithermark.com/resources/)

### Color distance calculation

- [X]  [Weighted Euclidean](https://www.compuphase.com/cmetric.htm)
- [X]  [Euclidean BT.709](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [Manhattan](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [Manhattan BT.709](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [Manhattan Nommyde](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [CIEDE2000](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [CIE94-Textiles](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [CIE94-GraphicArts](https://github.com/ibezkrovnyi/image-quantization)
- [X]  [CompuPhase](https://www.compuphase.com/cmetric.htm)
- [X]  [PNGQuant](https://github.com/ibezkrovnyi/image-quantization)
- [ ]  [Linear RGB](https://matejlou.blog/2023/12/06/ordered-dithering-for-arbitrary-or-irregular-palettes/)
- [X]  Weighted YUV
- [X]  Weighted YCbCr

Further Links for this part:

- [Wikipedia](https://en.wikipedia.org/wiki/Color_difference)

### GIF Format

Due to the nature of this application, **AnythingToGif** requires fine-grained control over the bytes written to disk. This includes managing local palettes, transparency, frame delays, and frame disposal methods. To achieve this level of precision, the tool incorporates its own GIF writing code, built directly from the GIF specifications. This custom code ensures that every aspect of the GIF format is meticulously handled, allowing for the creation of high-color images and smooth animations. The output is rigorously checked using various GIF debugging tools, ensuring compatibility and optimal performance across different platforms and browsers.

- [X] [Uncompressed Images](https://github.com/Distrotech/libungif/blob/master/UNCOMPRESSED_GIF)
- [X] [LZW Compression](https://giflib.sourceforge.net/whatsinagif/lzw_image_data.html)
- [ ] [Optimized compression](https://create.stephan-brumme.com/flexigif-lossless-gif-lzw-optimization/)
- [X] Optimized frame-size and positioning

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
