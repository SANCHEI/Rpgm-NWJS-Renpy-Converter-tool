from PIL import Image, ImageDraw, ImageFont
import os

# Banner style - wide format for itch.io header
width, height = 1200, 400

# Create image with gradient background
img = Image.new('RGB', (width, height), color=(17, 19, 24))
draw = ImageDraw.Draw(img)

# Gradient background
for i in range(height):
    shade = int(17 + (i / height) * 15)
    draw.line([(0, i), (width, i)], fill=(shade, shade + 2, shade + 4))

# Accent colors
accent = (68, 197, 255)
success = (70, 204, 120)
pink = (255, 105, 180)
warning = (255, 183, 77)

# Left panel
draw.rectangle([0, 0, 400, height], fill=(25, 29, 36))

# Title
try:
    title_font = ImageFont.truetype("arial.ttf", 52)
    subtitle_font = ImageFont.truetype("arial.ttf", 24)
    feature_font = ImageFont.truetype("arial.ttf", 16)
    small_font = ImageFont.truetype("arial.ttf", 14)
except:
    title_font = ImageFont.load_default(size=32)
    subtitle_font = ImageFont.load_default(size=16)
    feature_font = ImageFont.load_default(size=10)
    small_font = ImageFont.load_default(size=8)

# Title on left panel
title = "Game Asset Tool"
bbox = draw.textbbox((0, 0), title, font=title_font)
title_width = bbox[2] - bbox[0]
draw.text(((400 - title_width) // 2, 40), title, font=title_font, fill=(255, 255, 255))

# Subtitle
subtitle = "Convert | Extract | Unlock"
bbox = draw.textbbox((0, 0), subtitle, font=subtitle_font)
sub_width = bbox[2] - bbox[0]
draw.text(((400 - sub_width) // 2, 110), subtitle, font=subtitle_font, fill=accent)

# Features on left panel
features = [
    (accent, "RPG Maker MV / MZ"),
    (success, "Unity Asset Extraction"),
    (pink, "Ren'Py RPA / Unlocker"),
    (warning, "Godot / XP3 / PAK"),
]

y = 180
for color, feature in features:
    draw.ellipse([35, y + 4, 45, y + 14], fill=color)
    draw.text((55, y), "* " + feature, font=feature_font, fill=(200, 200, 200))
    y += 38

# Auto-Detection separately
draw.ellipse([35, y + 4, 45, y + 14], fill=(255, 255, 255))
draw.text((55, y), "* Auto-Detection", font=feature_font, fill=(200, 200, 200))

# Right panel - logo/graphic
# Draw grid pattern
for x in range(400, width, 30):
    draw.line([(x, 0), (x, height)], fill=(30, 34, 41), width=1)
for y_line in range(0, height, 30):
    draw.line([(400, y_line), (width, y_line)], fill=(30, 34, 41), width=1)

# Central graphic - tool icon representation
center_x, center_y = 800, 200
radius = 80

# Outer circle
draw.ellipse([center_x - radius, center_y - radius, center_x + radius, center_y + radius],
              outline=accent, width=3)

# Inner gear
inner_radius = 50
draw.ellipse([center_x - inner_radius, center_y - inner_radius, center_x + inner_radius, center_y + inner_radius],
              fill=(25, 29, 36), outline=success, width=2)

# Cross lines in gear
draw.line([(center_x - 30, center_y), (center_x + 30, center_y)], fill=accent, width=3)
draw.line([(center_x, center_y - 30), (center_x, center_y + 30)], fill=accent, width=3)

# Small circles around gear
for angle in range(0, 360, 45):
    import math
    x = center_x + int(100 * math.cos(math.radians(angle)))
    y = center_y + int(100 * math.sin(math.radians(angle)))
    draw.ellipse([x - 8, y - 8, x + 8, y + 8], fill=success)

# Bottom text
bottom_text = "No dependencies required"
bbox = draw.textbbox((0, 0), bottom_text, font=small_font)
bt_width = bbox[2] - bbox[0]
draw.text(((width - bt_width) // 2, height - 30), bottom_text, font=small_font, fill=(100, 100, 100))

# Save
output_path = r"D:\123123\converter_WinForms\itch\cover.png"
img.save(output_path, optimize=True)
print(f"Cover created: {output_path}")
print(f"Size: {width}x{height}")
