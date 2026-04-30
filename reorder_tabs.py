import re

# Read the file
with open('MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# Find all TabItems with their content
pattern = r'(<!-- [^>]+ Tab -->.*?</TabItem>)'
matches = re.findall(pattern, content, re.DOTALL)

# Tab names in order
tab_names = []
for match in matches:
    # Extract tab name
    name_match = re.search(r'<!-- ([^>]+) Tab -->', match)
    if name_match:
        tab_names.append(name_match.group(1))

print("Current order:")
for i, name in enumerate(tab_names, 1):
    print(f"{i}. {name}")

# Desired order mapping
desired_order = [
    "Server Status",
    "Console",
    "Events",
    "Account Management",
    "Players",
    "Server Analytics",
    "Communication",
    "Settings",
    "Config"
]

# Reorder the matches
reordered_matches = []
for desired_name in desired_order:
    for i, match in enumerate(matches):
        name_match = re.search(r'<!-- ([^>]+) Tab -->', match)
        if name_match and name_match.group(1) == desired_name:
            reordered_matches.append(matches.pop(i))
            break

# Rebuild the content
new_content = content
# Find the TabControl section
tab_control_start = content.find('<TabControl x:Name="MainTabControl"')
tab_control_end = content.find('</TabControl>') + len('</TabControl>')

before = content[:tab_control_start]
after = content[tab_control_end:]

# Build new TabControl content
new_tab_control = '<TabControl x:Name="MainTabControl" Grid.Row="1" Margin="10">\n'
for match in reordered_matches:
    new_tab_control += match + '\n\n'
new_tab_control += '</TabControl>'

new_content = before + new_tab_control + after

# Write the file
with open('MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.write(new_content)

print("\nReordered to:")
for i, name in enumerate(desired_order, 1):
    print(f"{i}. {name}")
