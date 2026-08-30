-- Build one coherent document from Markdown files that live in different
-- directories. Source markers are inserted by build-pdf.sh.

local marker_prefix = "SCPU_PDF_SOURCE_BOUNDARY::"

local emojis = {
    ["🧩"] = true,
    ["🚀"] = true,
    ["⚙️"] = true,
    ["💻"] = true,
    ["🧱"] = true,
    ["🧪"] = true,
    ["🔧"] = true,
    ["🌐"] = true,
    ["🧠"] = true,
    ["🧾"] = true,
    ["💾"] = true,
    ["⏱"] = true,
    ["🔹"] = true,
    ["💡"] = true,
    ["🌍"] = true,
    ["🛠️"] = true,
    ["📜"] = true
}

local function style_header_emojis(header)
    for index, inline in ipairs(header.content) do
        if inline.t == "Str" and emojis[inline.text] then
            header.content[index] = pandoc.Span(
                {inline},
                pandoc.Attr("", {"emoji"})
            )
        end
    end
    return header
end

local function normalize_path(path)
    path = path:gsub("\\", "/"):gsub("^%./", "")
    local parts = {}

    for part in path:gmatch("[^/]+") do
        if part == ".." then
            if #parts > 0 then
                table.remove(parts)
            end
        elseif part ~= "." and part ~= "" then
            table.insert(parts, part)
        end
    end

    return table.concat(parts, "/")
end

local function dirname(path)
    return path:match("^(.*)/[^/]*$") or ""
end

local function is_external(target)
    return target:match("^[%a][%w+.-]*:") or target:match("^//")
end

local function heading_slug(header)
    local text = pandoc.utils.stringify(header.content):lower()
    text = text:gsub("[’']", "")
    text = text:gsub("[^%w%s_-]", " ")
    text = text:gsub("[%s_]+", "-")
    text = text:gsub("%-+", "-")
    text = text:gsub("^%-+", ""):gsub("%-+$", "")
    text = text:gsub("^[^%a]+", "")
    return text ~= "" and text or "section"
end

local function html_escape(text)
    return text
        :gsub("&", "&amp;")
        :gsub("<", "&lt;")
        :gsub(">", "&gt;")
        :gsub('"', "&quot;")
end

local function resolve_document(target, source, lookup)
    local path = target:match("^([^#?]+)")
    if not path or path == "" then
        return nil
    end

    local resolved = normalize_path(dirname(source) .. "/" .. path)
    local document = lookup[resolved:lower()]

    if not document and not resolved:lower():match("%.md$") then
        document = lookup[normalize_path(resolved .. "/README.md"):lower()]
    end

    return document
end

local function resolve_anchor(document, fragment)
    if document.anchors[fragment] then
        return document.anchors[fragment]
    end

    -- Some repository links use a short semantic fragment (for example
    -- #automation for "One-shot commands and automation").
    local match = nil
    for slug, identifier in pairs(document.anchors) do
        if slug:find(fragment, 1, true) then
            if match then
                return nil
            end
            match = identifier
        end
    end
    return match
end

local function build_toc(documents)
    local html = {
        '<nav id="TOC" role="doc-toc">',
        '<h2 id="toc-title">Table of contents</h2>',
        '<ul>'
    }

    for index = 2, #documents do
        for _, heading in ipairs(documents[index].headings) do
            if heading.level <= 3 then
                table.insert(
                    html,
                    string.format(
                        '<li class="toc-level-%d"><a href="#%s">%s</a></li>',
                        heading.level,
                        html_escape(heading.id),
                        html_escape(heading.title)
                    )
                )
            end
        end
    end

    table.insert(html, "</ul>")
    table.insert(html, "</nav>")
    return pandoc.RawBlock("html", table.concat(html, "\n"))
end

function Pandoc(document)
    local documents = {}
    local current = nil

    for _, block in ipairs(document.blocks) do
        local source = nil
        if block.t == "Para" or block.t == "Plain" then
            local text = pandoc.utils.stringify(block.content)
            if text:sub(1, #marker_prefix) == marker_prefix then
                source = text:sub(#marker_prefix + 1)
            end
        end

        if source then
            current = {
                source = normalize_path(source),
                blocks = {},
                headings = {},
                anchors = {}
            }
            current.id = string.format("document-%03d", #documents + 1)
            table.insert(documents, current)
        elseif current then
            table.insert(current.blocks, block)
        end
    end

    if #documents == 0 then
        return document
    end

    local lookup = {}
    for _, item in ipairs(documents) do
        lookup[item.source:lower()] = item
    end

    -- Assign stable, document-scoped heading identifiers before resolving links.
    for _, item in ipairs(documents) do
        local occurrences = {}
        local container = pandoc.Div(item.blocks)

        container = pandoc.walk_block(container, {
            Header = function(header)
                header = style_header_emojis(header)
                local slug = heading_slug(header)
                local count = occurrences[slug] or 0
                occurrences[slug] = count + 1
                if count > 0 then
                    slug = slug .. "-" .. count
                end

                header.identifier = item.id .. "--" .. slug
                item.anchors[slug] = header.identifier
                table.insert(item.headings, {
                    level = header.level,
                    id = header.identifier,
                    title = pandoc.utils.stringify(header.content)
                })
                return header
            end
        })

        item.blocks = container.content
    end

    -- Resolve images from their source README and turn included Markdown links
    -- into PDF anchors.
    for index, item in ipairs(documents) do
        local container = pandoc.Div(item.blocks)

        container = pandoc.walk_block(container, {
            Image = function(image)
                if not is_external(image.src) and not image.src:match("^/") then
                    image.src = normalize_path(dirname(item.source) .. "/" .. image.src)
                end
                return image
            end,

            Link = function(link)
                local target = link.target

                if is_external(target) or target:match("^/") then
                    return link
                end

                if target:match("^#") then
                    local fragment = target:sub(2)
                    local anchor = resolve_anchor(item, fragment)
                    if anchor then
                        link.target = "#" .. anchor
                    end
                    return link
                end

                local target_document = resolve_document(target, item.source, lookup)
                if target_document then
                    local fragment = target:match("#(.+)$")
                    link.target = "#" .. target_document.id
                    if fragment and fragment ~= "" then
                        local anchor = resolve_anchor(target_document, fragment)
                        if anchor then
                            link.target = "#" .. anchor
                        end
                    end
                    return link
                end

                local path, fragment = target:match("^([^#]*)(#?.*)$")
                link.target = normalize_path(dirname(item.source) .. "/" .. path) .. fragment
                return link
            end
        })

        local classes = index == 1 and {"pdf-overview"} or {"pdf-chapter"}
        item.div = pandoc.Div(
            container.content,
            pandoc.Attr(item.id, classes, {{"data-source", item.source}})
        )
    end

    local output = {documents[1].div}
    if #documents > 1 then
        table.insert(output, build_toc(documents))
    end
    for index = 2, #documents do
        table.insert(output, documents[index].div)
    end

    document.blocks = output
    return document
end
