# Post-processing Stack for Universal Render Pipeline

This package adds the ability to create custom post-processing effects for the universal render pipeline in a manner similar to [PPSv2](https://github.com/Unity-Technologies/PostProcessing) and [HDRP's Custom Post Process](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@8.2/manual/Custom-Post-Process.html). It is supposed to be a replacement for Unity's **PPSv2** till URP internally supports custom post-processing effect.

## System Requirements

* Unity 2020.1
* URP 8.2.0

## Screenshots

![Scene-Screenshot-1](Documentation~/scene-screenshot-1.png)
![Scene-Screenshot-2](Documentation~/scene-screenshot-2.png)

The screenshots uses the following builtin effects:
* Tonemapping
* Vignette
* Film Grain
* Split Toning

They also contain the following custom effects:
* Edge Detection (Adapted from [this tutorial](https://halisavakis.com/my-take-on-shaders-edge-detection-image-effect/) by [Harry Alisavakis](https://halisavakis.com/)).
* Gradient Fog (A simplified version of [Multi-Colored Fog](https://halisavakis.com/my-take-on-shaders-firewatch-multi-colored-fog/) by [Harry Alisavakis](https://halisavakis.com/)).
* Chromatic Splitting.
* Streak (Adapted from [Kino](https://github.com/keijiro/Kino) by [Keijiro Takahashi](https://github.com/keijiro)).

## How To Install

**TODO**

## Tutorial

**TODO**

## License
 [MIT License](LICENSE.md)