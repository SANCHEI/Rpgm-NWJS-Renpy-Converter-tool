import imageio
import PIL.Image
import os

video_path = r"D:\123123\converter_WinForms\itch\demo_unity.mp4"
output_path = r"D:\123123\converter_WinForms\itch\demo_unity.gif"

# Crop settings from user (x1, y1, x2, y2)
x1, y1, x2, y2 = 166, 98, 1423, 955
crop_box = (x1, y1, x2, y2)

print(f"Reading video: {video_path}")
reader = imageio.get_reader(video_path)
frames = []
for frame in reader:
    frames.append(frame)
reader.close()
print(f"Total frames: {len(frames)}")

# Take 60 frames for good quality
step = max(1, len(frames) // 60)
selected = frames[::step][:60]
print(f"Selected {len(selected)} frames")

images = []
for f in selected:
    # Crop the frame
    img = PIL.Image.fromarray(f).crop(crop_box)
    # Resize to keep quality but reduce size
    img = img.resize((960, 650), PIL.Image.LANCZOS)
    img = img.convert('RGB').convert('P', palette=PIL.Image.ADAPTIVE, colors=256)
    images.append(img)

imageio.mimsave(output_path, images, fps=12, loop=0)
print(f"GIF created: {output_path}")

size_mb = os.path.getsize(output_path) / (1024 * 1024)
print(f"File size: {size_mb:.2f} MB")
