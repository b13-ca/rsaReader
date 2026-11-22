namespace rsaReader;

internal static class RealEditor {
    public const string Script = """
        <script>
        (function () {
          var savedRange = null;
          var syncTimer = 0;

          function rememberSelection() {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return;
            var node = sel.anchorNode;
            if (!node || !document.body.contains(node)) return;
            savedRange = sel.getRangeAt(0).cloneRange();
          }

          function prepareEdit() {
            if (document.body.contentEditable !== "true") return false;
            document.body.focus();
            if (!savedRange) return true;
            var sel = window.getSelection();
            sel.removeAllRanges();
            try { sel.addRange(savedRange); } catch (e) { return true; }
            return true;
          }

          function afterEdit() {
            rememberSelection();
            scheduleSync();
          }

          function scheduleSync() {
            if (document.body.contentEditable !== "true") return;
            if (syncTimer) clearTimeout(syncTimer);
            syncTimer = setTimeout(function () {
              syncTimer = 0;
              pushMarkdown(toMarkdown(document.body));
            }, 80);
          }

          function pushMarkdown(markdown) {
            try {
              if (window.rsaReal) rsaReal.syncMarkdown(markdown);
            } catch (e) {}
          }

          function escapeHtml(value) {
            return String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
          }

          function escapeAttr(value) {
            return escapeHtml(value).replace(/"/g, "&quot;");
          }

          function escapeText(value) {
            return String(value).replace(/\\/g, "\\\\").replace(/([*`_\[\]])/g, "\\$1");
          }

          function cleanUrl(value) {
            return String(value).replace(/ /g, "%20").replace(/\)/g, "%29");
          }

          function inline(node) {
            var out = "";
            for (var i = 0; i < node.childNodes.length; i++)
              out += inlineNode(node.childNodes[i]);
            return out;
          }

          function inlineNode(node) {
            if (node.nodeType === 3) return escapeText(node.textContent);
            if (node.nodeType !== 1) return "";
            var tag = node.tagName;
            if (tag === "BR") return "  \n";
            if (tag === "STRONG" || tag === "B") return wrap("**", inline(node));
            if (tag === "EM" || tag === "I") return wrap("*", inline(node));
            if (tag === "DEL" || tag === "S" || tag === "STRIKE") return wrap("~~", inline(node));
            if (tag === "CODE") return "`" + node.textContent.replace(/`/g, "") + "`";
            if (tag === "A") return "[" + inline(node) + "](" + cleanUrl(node.getAttribute("href") || "") + ")";
            if (tag === "IMG") return "![" + (node.getAttribute("alt") || "") + "](" + cleanUrl(node.getAttribute("src") || "") + ")";
            if (tag === "INPUT" || tag === "UL" || tag === "OL" || tag === "PRE" || tag === "TABLE" || tag === "BLOCKQUOTE") return "";
            return inline(node);
          }

          function wrap(marker, text) {
            if (!text) return "";
            return marker + text + marker;
          }

          function isBlank(node) {
            if (node.querySelector && node.querySelector("img")) return false;
            return !node.textContent.replace(/\u00a0/g, " ").trim();
          }

          function trimBlock(text) {
            var lines = text.split("\n");
            while (lines.length && lines[0].trim() === "") lines.shift();
            while (lines.length && lines[lines.length - 1].trim() === "") lines.pop();
            for (var i = 0; i < lines.length; i++) {
              if (lines[i].slice(-2) === "  ")
                lines[i] = lines[i].replace(/^[ \t]+/, "").replace(/[ \t]+$/, "  ");
              else
                lines[i] = lines[i].trim();
            }
            return lines.join("\n");
          }

          function taskBox(li) {
            for (var i = 0; i < li.childNodes.length; i++) {
              var child = li.childNodes[i];
              if (child.nodeType !== 1) continue;
              if (child.tagName === "INPUT" && child.type === "checkbox") return child;
              if (child.tagName === "P") {
                var nested = child.querySelector("input[type=checkbox]");
                if (nested) return nested;
              }
            }
            return null;
          }

          function listToMd(node, ordered, indent) {
            var lines = [];
            var n = 1;
            for (var i = 0; i < node.children.length; i++) {
              var li = node.children[i];
              if (!li || li.tagName !== "LI") continue;
              var box = taskBox(li);
              var marker = box ? (box.checked ? "- [x] " : "- [ ] ") : (ordered ? (n + ". ") : "- ");
              n++;
              var textParts = [];
              var nested = [];
              for (var j = 0; j < li.childNodes.length; j++) {
                var childNode = li.childNodes[j];
                if (childNode.nodeType === 1 && (childNode.tagName === "UL" || childNode.tagName === "OL"))
                  nested.push(listToMd(childNode, childNode.tagName === "OL", indent + "  "));
                else if (childNode.nodeType === 1 && childNode.tagName === "INPUT")
                  continue;
                else
                  textParts.push(inlineNode(childNode));
              }
              lines.push(indent + marker + textParts.join("").replace(/\n+/g, " ").trim());
              for (var k = 0; k < nested.length; k++)
                if (nested[k]) lines.push(nested[k]);
            }
            return lines.join("\n");
          }

          function fence(node) {
            var code = node.tagName === "CODE" ? node : node.querySelector("code");
            var text = (code || node).textContent.replace(/\n$/, "");
            var lang = "";
            if (code && code.className) {
              var match = /language-([\w+-]+)/.exec(code.className);
              if (match) lang = match[1];
            }
            return "```" + lang + "\n" + text + "\n```\n\n";
          }

          function quote(node) {
            var parts = [];
            for (var i = 0; i < node.childNodes.length; i++) {
              var piece = blockNode(node.childNodes[i]);
              if (piece) parts.push(piece);
            }
            var text = tidy(parts.join(""));
            if (!text) return "";
            return text.split("\n").map(function (line) { return "> " + line; }).join("\n") + "\n\n";
          }

          function tableToMd(table) {
            var rows = [];
            var trs = table.querySelectorAll("tr");
            for (var i = 0; i < trs.length; i++) {
              var cells = trs[i].querySelectorAll("th,td");
              var values = [];
              for (var c = 0; c < cells.length; c++)
                values.push(inline(cells[c]).trim().replace(/\|/g, "\\|").replace(/\n/g, " "));
              rows.push("| " + values.join(" | ") + " |");
              if (i === 0)
                rows.push("| " + values.map(function () { return "---"; }).join(" | ") + " |");
            }
            return rows.join("\n") + "\n\n";
          }

          function blockNode(node) {
            if (node.nodeType === 3) {
              if (!node.textContent.trim()) return "";
              return escapeText(node.textContent).trim() + "\n\n";
            }
            if (node.nodeType !== 1) return "";
            var tag = node.tagName;
            if (tag === "SCRIPT" || tag === "STYLE") return "";
            if (/^H[1-6]$/.test(tag))
              return "#".repeat(Number(tag.charAt(1))) + " " + inline(node).trim() + "\n\n";
            if (tag === "P" || tag === "DIV") {
              if (isBlank(node)) return "\n";
              return trimBlock(inline(node)) + "\n\n";
            }
            if (tag === "UL") return listToMd(node, false, "") + "\n\n";
            if (tag === "OL") return listToMd(node, true, "") + "\n\n";
            if (tag === "PRE") return fence(node);
            if (tag === "BLOCKQUOTE") return quote(node);
            if (tag === "HR") return "---\n\n";
            if (tag === "TABLE") return tableToMd(node);
            if (tag === "BR") return "\n";
            var text = trimBlock(inline(node));
            return text ? text + "\n\n" : "";
          }

          function tidy(markdown) {
            return markdown.replace(/\n{3,}/g, "\n\n").replace(/^\n+/, "").replace(/\n+$/, "");
          }

          function toMarkdown(root) {
            var parts = [];
            for (var i = 0; i < root.childNodes.length; i++) {
              var piece = blockNode(root.childNodes[i]);
              if (piece) parts.push(piece);
            }
            return tidy(parts.join(""));
          }

          window.selectionText = function () {
            if (savedRange) return savedRange.toString();
            var sel = window.getSelection();
            return sel ? sel.toString() : "";
          };

          window.flushMarkdown = function () {
            if (syncTimer) {
              clearTimeout(syncTimer);
              syncTimer = 0;
            }
            return toMarkdown(document.body);
          };

          window.setEditable = function (on) {
            document.body.contentEditable = on ? "true" : "false";
            if (!on) return;
            try {
              document.execCommand("defaultParagraphSeparator", false, "p");
              document.execCommand("styleWithCSS", false, false);
            } catch (e) {}
            document.body.focus();
          };

          window.cmdBold = function () {
            if (!prepareEdit()) return;
            document.execCommand("styleWithCSS", false, false);
            document.execCommand("bold", false, null);
            afterEdit();
          };

          window.cmdH1 = function () {
            if (!prepareEdit()) return;
            document.execCommand("formatBlock", false, "h1");
            afterEdit();
          };

          window.cmdH2 = function () {
            if (!prepareEdit()) return;
            document.execCommand("formatBlock", false, "h2");
            afterEdit();
          };

          window.cmdBullet = function () {
            if (!prepareEdit()) return;
            document.execCommand("insertUnorderedList", false, null);
            afterEdit();
          };

          window.cmdBreak = function () {
            if (!prepareEdit()) return;
            document.execCommand("insertHTML", false, "<br>");
            afterEdit();
          };

          window.cmdLink = function (definition, url) {
            if (!prepareEdit()) return;
            document.execCommand("insertHTML", false, '<a href="' + escapeAttr(url) + '">' + escapeHtml(definition) + "</a>");
            afterEdit();
          };

          window.cmdImage = function (definition, url) {
            if (!prepareEdit()) return;
            document.execCommand("insertHTML", false, '<img alt="' + escapeAttr(definition) + '" src="' + escapeAttr(url) + '">');
            afterEdit();
          };

          window.cmdCode = function () {
            if (!prepareEdit()) return;
            var sel = window.getSelection();
            var text = sel ? sel.toString() : "";
            if (sel && sel.rangeCount) sel.getRangeAt(0).deleteContents();
            var pre = document.createElement("pre");
            var code = document.createElement("code");
            code.textContent = text;
            pre.appendChild(code);
            var anchor = sel && sel.anchorNode;
            var block = anchor && anchor.nodeType === 1 ? anchor : anchor && anchor.parentNode;
            while (block && block !== document.body && block.parentNode !== document.body)
              block = block.parentNode;
            if (block && block.parentNode === document.body)
              block.parentNode.insertBefore(pre, block.nextSibling);
            else
              document.body.appendChild(pre);
            var caret = document.createRange();
            caret.selectNodeContents(code);
            caret.collapse(text.length === 0);
            if (sel) {
              sel.removeAllRanges();
              sel.addRange(caret);
            }
            afterEdit();
          };

          document.addEventListener("mouseup", rememberSelection);
          document.addEventListener("keyup", function (e) {
            rememberSelection();
            if (document.body.contentEditable === "true" && (e.ctrlKey || e.metaKey))
              scheduleSync();
          });
          document.addEventListener("input", scheduleSync);
          document.addEventListener("click", function (e) {
            var el = e.target;
            if (el && el.closest && el.closest("a")) e.preventDefault();
          });
          document.addEventListener("keydown", function (e) {
            if (!(e.ctrlKey || e.metaKey) || !window.rsaReal) return;
            if (e.key === "s" || e.key === "S") {
              e.preventDefault();
              if (syncTimer) { clearTimeout(syncTimer); syncTimer = 0; }
              pushMarkdown(toMarkdown(document.body));
              rsaReal.save();
            } else if (e.key === "o" || e.key === "O") {
              e.preventDefault();
              if (syncTimer) { clearTimeout(syncTimer); syncTimer = 0; }
              pushMarkdown(toMarkdown(document.body));
              rsaReal.load();
            }
          });
        })();
        </script>
        """;
}
